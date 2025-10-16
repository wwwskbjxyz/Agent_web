#pragma once

//
// cloud_crypto.h
// -----------------
// 封装了云端通讯的加密与完整性校验逻辑。
// 为避免重复代码, 客户端与服务端共享该头文件。
//

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <random>
#include <stdexcept>
#include <string>
#include <vector>

namespace sp
{
    namespace crypto
    {
        // ========================= SHA-256 实现 =========================
        // 轻量实现, 仅用于 key 派生和 HMAC 计算。
        struct Sha256Context
        {
            std::array<std::uint32_t, 8> state{};
            std::array<std::uint8_t, 64> buffer{};
            std::uint64_t bit_length = 0;
            std::size_t buffer_length = 0;
        };

        static constexpr std::uint32_t kSha256InitialState[8] = {
            0x6a09e667u, 0xbb67ae85u, 0x3c6ef372u, 0xa54ff53au,
            0x510e527fu, 0x9b05688cu, 0x1f83d9abu, 0x5be0cd19u
        };

        static constexpr std::uint32_t kSha256K[64] = {
            0x428a2f98u, 0x71374491u, 0xb5c0fbcfu, 0xe9b5dba5u,
            0x3956c25bu, 0x59f111f1u, 0x923f82a4u, 0xab1c5ed5u,
            0xd807aa98u, 0x12835b01u, 0x243185beu, 0x550c7dc3u,
            0x72be5d74u, 0x80deb1feu, 0x9bdc06a7u, 0xc19bf174u,
            0xe49b69c1u, 0xefbe4786u, 0x0fc19dc6u, 0x240ca1ccu,
            0x2de92c6fu, 0x4a7484aau, 0x5cb0a9dcu, 0x76f988dau,
            0x983e5152u, 0xa831c66du, 0xb00327c8u, 0xbf597fc7u,
            0xc6e00bf3u, 0xd5a79147u, 0x06ca6351u, 0x14292967u,
            0x27b70a85u, 0x2e1b2138u, 0x4d2c6dfcu, 0x53380d13u,
            0x650a7354u, 0x766a0abbu, 0x81c2c92eu, 0x92722c85u,
            0xa2bfe8a1u, 0xa81a664bu, 0xc24b8b70u, 0xc76c51a3u,
            0xd192e819u, 0xd6990624u, 0xf40e3585u, 0x106aa070u,
            0x19a4c116u, 0x1e376c08u, 0x2748774cu, 0x34b0bcb5u,
            0x391c0cb3u, 0x4ed8aa4au, 0x5b9cca4fu, 0x682e6ff3u,
            0x748f82eeu, 0x78a5636fu, 0x84c87814u, 0x8cc70208u,
            0x90befffau, 0xa4506cebu, 0xbef9a3f7u, 0xc67178f2u
        };

        inline std::uint32_t rotr(std::uint32_t value, std::uint32_t bits)
        {
            return (value >> bits) | (value << (32u - bits));
        }

        inline void sha256_init(Sha256Context& ctx)
        {
            std::memcpy(ctx.state.data(), kSha256InitialState, sizeof(kSha256InitialState));
            ctx.bit_length = 0;
            ctx.buffer_length = 0;
        }

        inline void sha256_process_block(Sha256Context& ctx, const std::uint8_t block[64])
        {
            std::uint32_t w[64];
            for (int i = 0; i < 16; ++i)
            {
                w[i] = (static_cast<std::uint32_t>(block[i * 4]) << 24) |
                       (static_cast<std::uint32_t>(block[i * 4 + 1]) << 16) |
                       (static_cast<std::uint32_t>(block[i * 4 + 2]) << 8) |
                       (static_cast<std::uint32_t>(block[i * 4 + 3]));
            }
            for (int i = 16; i < 64; ++i)
            {
                std::uint32_t s0 = rotr(w[i - 15], 7) ^ rotr(w[i - 15], 18) ^ (w[i - 15] >> 3);
                std::uint32_t s1 = rotr(w[i - 2], 17) ^ rotr(w[i - 2], 19) ^ (w[i - 2] >> 10);
                w[i] = w[i - 16] + s0 + w[i - 7] + s1;
            }

            std::uint32_t a = ctx.state[0];
            std::uint32_t b = ctx.state[1];
            std::uint32_t c = ctx.state[2];
            std::uint32_t d = ctx.state[3];
            std::uint32_t e = ctx.state[4];
            std::uint32_t f = ctx.state[5];
            std::uint32_t g = ctx.state[6];
            std::uint32_t h = ctx.state[7];

            for (int i = 0; i < 64; ++i)
            {
                std::uint32_t S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25);
                std::uint32_t ch = (e & f) ^ ((~e) & g);
                std::uint32_t temp1 = h + S1 + ch + kSha256K[i] + w[i];
                std::uint32_t S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22);
                std::uint32_t maj = (a & b) ^ (a & c) ^ (b & c);
                std::uint32_t temp2 = S0 + maj;

                h = g;
                g = f;
                f = e;
                e = d + temp1;
                d = c;
                c = b;
                b = a;
                a = temp1 + temp2;
            }

            ctx.state[0] += a;
            ctx.state[1] += b;
            ctx.state[2] += c;
            ctx.state[3] += d;
            ctx.state[4] += e;
            ctx.state[5] += f;
            ctx.state[6] += g;
            ctx.state[7] += h;
        }

        inline void sha256_update(Sha256Context& ctx, const std::uint8_t* data, std::size_t length)
        {
            ctx.bit_length += static_cast<std::uint64_t>(length) * 8ull;
            std::size_t offset = 0;
            while (length > 0)
            {
                std::size_t copy = std::min<std::size_t>(64 - ctx.buffer_length, length);
                std::memcpy(ctx.buffer.data() + ctx.buffer_length, data + offset, copy);
                ctx.buffer_length += copy;
                offset += copy;
                length -= copy;

                if (ctx.buffer_length == 64)
                {
                    sha256_process_block(ctx, ctx.buffer.data());
                    ctx.buffer_length = 0;
                }
            }
        }

        inline void sha256_final(Sha256Context& ctx, std::uint8_t digest[32])
        {
            // 添加末尾的 1 bit 和 0 填充
            ctx.buffer[ctx.buffer_length++] = 0x80u;
            if (ctx.buffer_length > 56)
            {
                while (ctx.buffer_length < 64)
                {
                    ctx.buffer[ctx.buffer_length++] = 0u;
                }
                sha256_process_block(ctx, ctx.buffer.data());
                ctx.buffer_length = 0;
            }

            while (ctx.buffer_length < 56)
            {
                ctx.buffer[ctx.buffer_length++] = 0u;
            }

            // 写入消息长度 (单位 bit)
            for (int i = 7; i >= 0; --i)
            {
                ctx.buffer[ctx.buffer_length++] = static_cast<std::uint8_t>((ctx.bit_length >> (i * 8)) & 0xFFu);
            }
            sha256_process_block(ctx, ctx.buffer.data());

            for (int i = 0; i < 8; ++i)
            {
                digest[i * 4 + 0] = static_cast<std::uint8_t>((ctx.state[i] >> 24) & 0xFFu);
                digest[i * 4 + 1] = static_cast<std::uint8_t>((ctx.state[i] >> 16) & 0xFFu);
                digest[i * 4 + 2] = static_cast<std::uint8_t>((ctx.state[i] >> 8) & 0xFFu);
                digest[i * 4 + 3] = static_cast<std::uint8_t>((ctx.state[i]) & 0xFFu);
            }
        }

        inline void compute_sha256(const std::uint8_t* data, std::size_t length, std::uint8_t digest[32])
        {
            Sha256Context ctx;
            sha256_init(ctx);
            sha256_update(ctx, data, length);
            sha256_final(ctx, digest);
        }

        // ========================= TEA 算法实现 =========================
        inline void tea_encrypt_block(std::uint32_t v[2], const std::uint32_t k[4])
        {
            std::uint32_t v0 = v[0];
            std::uint32_t v1 = v[1];
            std::uint32_t sum = 0;
            constexpr std::uint32_t delta = 0x9E3779B9u;
            for (int i = 0; i < 32; ++i)
            {
                sum += delta;
                v0 += ((v1 << 4) + k[0]) ^ (v1 + sum) ^ ((v1 >> 5) + k[1]);
                v1 += ((v0 << 4) + k[2]) ^ (v0 + sum) ^ ((v0 >> 5) + k[3]);
            }
            v[0] = v0;
            v[1] = v1;
        }

        inline void tea_decrypt_block(std::uint32_t v[2], const std::uint32_t k[4])
        {
            std::uint32_t v0 = v[0];
            std::uint32_t v1 = v[1];
            constexpr std::uint32_t delta = 0x9E3779B9u;
            std::uint32_t sum = delta * 32;
            for (int i = 0; i < 32; ++i)
            {
                v1 -= ((v0 << 4) + k[2]) ^ (v0 + sum) ^ ((v0 >> 5) + k[3]);
                v0 -= ((v1 << 4) + k[0]) ^ (v1 + sum) ^ ((v1 >> 5) + k[1]);
                sum -= delta;
            }
            v[0] = v0;
            v[1] = v1;
        }

        inline void apply_pkcs7_padding(std::vector<std::uint8_t>& data)
        {
            std::uint8_t pad = static_cast<std::uint8_t>(8 - (data.size() % 8));
            if (pad == 0)
            {
                pad = 8;
            }
            for (std::uint8_t i = 0; i < pad; ++i)
            {
                data.push_back(pad);
            }
        }

        inline void remove_pkcs7_padding(std::vector<std::uint8_t>& data)
        {
            if (data.empty())
            {
                throw std::runtime_error("Padding removal on empty buffer");
            }
            std::uint8_t pad = data.back();
            if (pad == 0 || pad > 8 || pad > data.size())
            {
                throw std::runtime_error("Invalid padding value");
            }
            data.resize(data.size() - pad);
        }

        inline void encrypt_buffer(std::vector<std::uint8_t>& data, const std::uint32_t key[4])
        {
            apply_pkcs7_padding(data);
            for (std::size_t offset = 0; offset < data.size(); offset += 8)
            {
                std::uint32_t block[2];
                std::memcpy(block, data.data() + offset, sizeof(block));
                tea_encrypt_block(block, key);
                std::memcpy(data.data() + offset, block, sizeof(block));
            }
        }

        inline void decrypt_buffer(std::vector<std::uint8_t>& data, const std::uint32_t key[4])
        {
            if (data.size() % 8 != 0)
            {
                throw std::runtime_error("Encrypted buffer size must be multiple of 8");
            }
            for (std::size_t offset = 0; offset < data.size(); offset += 8)
            {
                std::uint32_t block[2];
                std::memcpy(block, data.data() + offset, sizeof(block));
                tea_decrypt_block(block, key);
                std::memcpy(data.data() + offset, block, sizeof(block));
            }
            remove_pkcs7_padding(data);
        }

        inline void derive_key_material(const std::string& card, std::uint64_t nonce,
                                        std::uint32_t key[4], std::uint8_t hmac_key[32])
        {
            std::string input = card + ":" + std::to_string(nonce);
            std::uint8_t digest[32];
            compute_sha256(reinterpret_cast<const std::uint8_t*>(input.data()), input.size(), digest);
            for (int i = 0; i < 4; ++i)
            {
                key[i] = (static_cast<std::uint32_t>(digest[i * 4 + 0]) << 24) |
                         (static_cast<std::uint32_t>(digest[i * 4 + 1]) << 16) |
                         (static_cast<std::uint32_t>(digest[i * 4 + 2]) << 8) |
                         (static_cast<std::uint32_t>(digest[i * 4 + 3]));
            }
            std::memcpy(hmac_key, digest, 32);
        }

        inline std::vector<std::uint8_t> hmac_sha256(const std::uint8_t* key, std::size_t key_length,
                                                     const std::vector<std::uint8_t>& data)
        {
            const std::size_t block_size = 64;
            std::array<std::uint8_t, block_size> key_block{};
            if (key_length > block_size)
            {
                compute_sha256(key, key_length, key_block.data());
                key_length = 32;
            }
            std::memcpy(key_block.data(), key, key_length);

            std::array<std::uint8_t, block_size> o_key_pad{};
            std::array<std::uint8_t, block_size> i_key_pad{};
            for (std::size_t i = 0; i < block_size; ++i)
            {
                o_key_pad[i] = key_block[i] ^ 0x5cu;
                i_key_pad[i] = key_block[i] ^ 0x36u;
            }

            std::vector<std::uint8_t> inner(block_size + data.size());
            std::memcpy(inner.data(), i_key_pad.data(), block_size);
            std::memcpy(inner.data() + block_size, data.data(), data.size());
            std::uint8_t inner_digest[32];
            compute_sha256(inner.data(), inner.size(), inner_digest);

            std::vector<std::uint8_t> outer(block_size + 32);
            std::memcpy(outer.data(), o_key_pad.data(), block_size);
            std::memcpy(outer.data() + block_size, inner_digest, 32);

            std::vector<std::uint8_t> result(32);
            compute_sha256(outer.data(), outer.size(), result.data());
            return result;
        }

        inline std::uint64_t random_nonce()
        {
            static thread_local std::mt19937_64 rng{std::random_device{}()};
            return rng();
        }

        inline std::vector<std::uint8_t> encrypt_packet(const std::string& card,
                                                         const std::vector<std::uint8_t>& payload)
        {
            std::uint64_t nonce = random_nonce();
            std::uint32_t key[4];
            std::uint8_t hmac_key[32];
            derive_key_material(card, nonce, key, hmac_key);

            std::vector<std::uint8_t> buffer = payload;
            encrypt_buffer(buffer, key);

            std::vector<std::uint8_t> packet;
            packet.reserve(8 + 4 + buffer.size() + 32);
            for (int i = 0; i < 8; ++i)
            {
                packet.push_back(static_cast<std::uint8_t>((nonce >> (i * 8)) & 0xFFu));
            }
            std::uint32_t size = static_cast<std::uint32_t>(buffer.size());
            for (int i = 0; i < 4; ++i)
            {
                packet.push_back(static_cast<std::uint8_t>((size >> (i * 8)) & 0xFFu));
            }
            packet.insert(packet.end(), buffer.begin(), buffer.end());

            // HMAC 校验
            std::vector<std::uint8_t> hmac_input(packet.begin(), packet.end());
            std::vector<std::uint8_t> hmac = hmac_sha256(hmac_key, 32, hmac_input);
            packet.insert(packet.end(), hmac.begin(), hmac.end());
            return packet;
        }

        inline std::vector<std::uint8_t> decrypt_packet(const std::string& card,
                                                         const std::vector<std::uint8_t>& packet)
        {
            if (packet.size() < 8 + 4 + 32)
            {
                throw std::runtime_error("Packet too small");
            }
            std::uint64_t nonce = 0;
            for (int i = 0; i < 8; ++i)
            {
                nonce |= static_cast<std::uint64_t>(packet[i]) << (i * 8);
            }
            std::uint32_t size = 0;
            for (int i = 0; i < 4; ++i)
            {
                size |= static_cast<std::uint32_t>(packet[8 + i]) << (i * 8);
            }
            if (packet.size() != 8 + 4 + size + 32)
            {
                throw std::runtime_error("Packet size mismatch");
            }

            std::uint32_t key[4];
            std::uint8_t hmac_key[32];
            derive_key_material(card, nonce, key, hmac_key);

            std::vector<std::uint8_t> hmac_input(packet.begin(), packet.begin() + 8 + 4 + size);
            std::vector<std::uint8_t> expected_hmac = hmac_sha256(hmac_key, 32, hmac_input);
            if (!std::equal(expected_hmac.begin(), expected_hmac.end(), packet.end() - 32))
            {
                throw std::runtime_error("HMAC verification failed");
            }

            std::vector<std::uint8_t> buffer(packet.begin() + 12, packet.begin() + 12 + size);
            decrypt_buffer(buffer, key);
            return buffer;
        }
    } // namespace crypto
} // namespace sp

