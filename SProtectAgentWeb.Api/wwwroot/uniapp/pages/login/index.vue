<template>
  <view class="page">
    <view class="glass-card hero">
      <view class="hero-content">
        <view>
          <text class="badge">SProtect Platform</text>
          <text class="title">多作者统一控制台</text>
          <text class="subtitle">
            支持代理集中登录、绑定多个软件码并通过主控后端安全转发请求。
          </text>
        </view>

        <view class="actions">
          <button class="action" @tap="goAgent">代理入口</button>
          <button class="action secondary" @tap="goAuthor">作者入口</button>
        </view>
      </view>

    </view>
  </view>
</template>

<script setup lang="ts">
function goAgent() {
  uni.navigateTo({ url: '/pages/login/agent' });
}

function goAuthor() {
  uni.navigateTo({ url: '/pages/login/author' });
}
</script>

<style scoped lang="scss">
.page {
  min-height: 100vh;
  padding: 80rpx 40rpx;
  display: flex;
  align-items: center;
  justify-content: center;
}

.hero {
  width: min(760rpx, 100%);
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 48rpx 40rpx;
}

.hero-content {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  text-align: left;
}

.badge {
  align-self: flex-start;
  padding: 12rpx 32rpx;
  border-radius: 999rpx;
  background: rgba(59, 130, 246, 0.16);
  color: rgba(96, 165, 250, 0.95);
  font-size: 24rpx;
  letter-spacing: 0.08em;
}

.title {
  font-size: 48rpx;
  font-weight: 600;
  color: #f8fafc;
}

.subtitle {
  font-size: 26rpx;
  color: rgba(226, 232, 240, 0.78);
  line-height: 1.6;
}

.actions {
  display: flex;
  flex-direction: row;
  flex-wrap: wrap;
  gap: 24rpx;
  margin-top: 8rpx;
}

.action {
  flex: 1;
  min-width: 220rpx;
  border-radius: 999rpx;
  padding: 22rpx 0;
  background: linear-gradient(135deg, #6366f1, #8b5cf6);
  color: #fff;
  font-size: 28rpx;
  font-weight: 600;
  border: none;
}

.action.secondary {
  background: rgba(148, 163, 184, 0.18);
  color: rgba(226, 232, 240, 0.95);
}

</style>
