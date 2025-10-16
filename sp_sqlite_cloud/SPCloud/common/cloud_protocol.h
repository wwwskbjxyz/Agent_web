#pragma once

//
// cloud_protocol.h
// ------------------
// 该头文件定义了客户端与服务端之间的二进制协议格式。
// 我们将远程调用封装为 CloudQuery 枚举以及一个简单的 TLV (type-length-value)
// 编码器/解码器，方便双方共享解析逻辑。
//

#include <cstdint>
#include <cstring>
#include <string>
#include <vector>
#include <stdexcept>

namespace sp
{
    // 协议版本号，便于后续升级时做向后兼容判断。
    static constexpr std::uint32_t kCloudProtocolVersion = 1u;

    // 用于区分不同云端指令的枚举。
    enum class CloudQuery : std::uint32_t
    {
        AgentGetAll = 1,
        AgentGetByUsername = 2,
        AgentSetStatus = 3,
        AgentUpdateRemark = 4,
    };

    // 简单的二进制写入器，负责把数值和字符串序列化到 buffer 中。
    class BinaryWriter
    {
    public:
        explicit BinaryWriter(std::vector<std::uint8_t>& buffer)
            : buffer_(buffer)
        {
        }

        void write_uint32(std::uint32_t value)
        {
            for (int i = 0; i < 4; ++i)
            {
                buffer_.push_back(static_cast<std::uint8_t>((value >> (i * 8)) & 0xFFu));
            }
        }

        void write_uint64(std::uint64_t value)
        {
            for (int i = 0; i < 8; ++i)
            {
                buffer_.push_back(static_cast<std::uint8_t>((value >> (i * 8)) & 0xFFu));
            }
        }

        void write_double(double value)
        {
            static_assert(sizeof(double) == sizeof(std::uint64_t), "Unexpected double size");
            std::uint64_t tmp = 0;
            std::memcpy(&tmp, &value, sizeof(tmp));
            write_uint64(tmp);
        }

        void write_int64(std::int64_t value)
        {
            write_uint64(static_cast<std::uint64_t>(value));
        }

        void write_int32(std::int32_t value)
        {
            write_uint32(static_cast<std::uint32_t>(value));
        }

        void write_string(const std::string& value)
        {
            write_uint32(static_cast<std::uint32_t>(value.size()));
            buffer_.insert(buffer_.end(), value.begin(), value.end());
        }

        void write_bytes(const std::vector<std::uint8_t>& value)
        {
            write_uint32(static_cast<std::uint32_t>(value.size()));
            buffer_.insert(buffer_.end(), value.begin(), value.end());
        }

    private:
        std::vector<std::uint8_t>& buffer_;
    };

    // 与 BinaryWriter 配套的读取器，用于从字节数组解析数据。
    class BinaryReader
    {
    public:
        BinaryReader(const std::uint8_t* begin, const std::uint8_t* end)
            : current_(begin), end_(end)
        {
        }

        std::uint32_t read_uint32()
        {
            ensure_bytes(4);
            std::uint32_t result = 0;
            for (int i = 0; i < 4; ++i)
            {
                result |= static_cast<std::uint32_t>(current_[i]) << (i * 8);
            }
            current_ += 4;
            return result;
        }

        std::uint64_t read_uint64()
        {
            ensure_bytes(8);
            std::uint64_t result = 0;
            for (int i = 0; i < 8; ++i)
            {
                result |= static_cast<std::uint64_t>(current_[i]) << (i * 8);
            }
            current_ += 8;
            return result;
        }

        double read_double()
        {
            std::uint64_t tmp = read_uint64();
            double value = 0.0;
            std::memcpy(&value, &tmp, sizeof(value));
            return value;
        }

        std::int64_t read_int64()
        {
            return static_cast<std::int64_t>(read_uint64());
        }

        std::int32_t read_int32()
        {
            return static_cast<std::int32_t>(read_uint32());
        }

        std::string read_string()
        {
            std::uint32_t length = read_uint32();
            ensure_bytes(length);
            const char* begin = reinterpret_cast<const char*>(current_);
            std::string result(begin, begin + length);
            current_ += length;
            return result;
        }

        std::vector<std::uint8_t> read_bytes()
        {
            std::uint32_t length = read_uint32();
            ensure_bytes(length);
            std::vector<std::uint8_t> result(current_, current_ + length);
            current_ += length;
            return result;
        }

        bool eof() const
        {
            return current_ >= end_;
        }

    private:
        void ensure_bytes(std::size_t count)
        {
            if (static_cast<std::size_t>(end_ - current_) < count)
            {
                throw std::runtime_error("BinaryReader: unexpected end of buffer");
            }
        }

        const std::uint8_t* current_;
        const std::uint8_t* end_;
    };
} // namespace sp

