<template>
  <view class="page">
    <view class="header glass-card">
      <view>
        <text class="title">即时沟通</text>
        <text class="subtitle">与渠道代理实时同步巡检与协同进度</text>
      </view>
      <view class="header-actions">
        <SoftwarePicker />
        <button class="header-btn" @tap="startDirectSession">发起单聊</button>
        <button class="header-btn ghost" @tap="openGroupDialog">创建群聊</button>
      </view>
    </view>

    <view class="chat-layout">
      <view class="session-panel glass-card">
        <view class="session-header">
          <text class="session-title">会话列表</text>
          <text class="session-counter">{{ sessions.length }} 个会话</text>
        </view>
        <view v-if="loadingSessions" class="session-empty">加载中...</view>
        <view v-else-if="!sessions.length" class="session-empty">暂无会话，点击右上角即可创建</view>
        <scroll-view v-else scroll-y class="session-list" :show-scrollbar="false">
          <view
            v-for="item in sessions"
            :key="item.id"
            :class="['session-item', { active: item.id === activeSessionId }]"
            @tap="selectSession(item.id)"
          >
            <view class="session-title-row">
              <text class="session-name">{{ item.title }}</text>
              <view v-if="item.unread" class="session-unread">{{ item.unread }}</view>
            </view>
            <text class="session-preview">{{ item.preview || '暂未开始对话' }}</text>
            <text class="session-time">{{ item.updatedAt }}</text>
          </view>
        </scroll-view>
      </view>

      <view class="chat-panel glass-card">
        <view v-if="!activeSession" class="chat-empty">
          请选择左侧会话或点击“新建会话”发起沟通
        </view>
        <view v-else class="chat-body">
          <view class="chat-meta">
            <view>
              <text class="chat-title">{{ activeSession.title }}</text>
              <text class="chat-subtitle">{{ activeSession.participants.join('、') }}</text>
            </view>
            <button class="chat-action" @tap="exportHistory">导出记录</button>
          </view>

          <scroll-view scroll-y class="message-list" :show-scrollbar="false" :scroll-into-view="lastMessageId">
            <view
              v-for="message in activeSession.messages"
              :key="message.id"
              :id="message.id"
              :class="['message', message.sender === 'user' ? 'self' : 'system']"
            >
              <template v-if="message.type === 'image'">
                <image
                  class="message-image"
                  mode="widthFix"
                  :src="message.content"
                  @tap="previewImage(message.content)"
                />
                <text
                  v-if="message.caption"
                  class="message-caption copyable"
                  @longpress="copyContent(message.caption)"
                  @tap.stop="copyContent(message.caption)"
                >
                  {{ message.caption }}
                </text>
              </template>
              <text
                v-else
                class="message-content copyable"
                @longpress="copyContent(message.content)"
                @tap.stop="copyContent(message.content)"
              >
                {{ message.content }}
              </text>
              <text
                class="message-time copyable"
                @longpress="copyContent(message.time)"
                @tap.stop="copyContent(message.time)"
              >
                {{ message.time }}
              </text>
            </view>
            <view v-if="loadingMessages" class="message-loading">消息加载中...</view>
          </scroll-view>

          <view class="composer glass-light">
            <view class="composer-toolbar">
              <button class="tool-btn" @tap="toggleEmojiPanel">😊</button>
              <button class="tool-btn" :disabled="sending || sendingImage" @tap="chooseImage">📎</button>
            </view>
            <textarea
              v-model="draft"
              class="composer-input"
              placeholder="输入消息内容，支持快捷同步巡检结果"
              :maxlength="500"
              auto-height
            />
            <view v-if="emojiPanelVisible" class="emoji-panel">
              <text v-for="emoji in emojiList" :key="emoji" class="emoji-item" @tap="insertEmoji(emoji)">{{ emoji }}</text>
            </view>
            <button class="composer-send" :disabled="!draft.trim() || sending || sendingImage" @tap="sendMessage">
              {{ sending ? '发送中...' : '发送消息' }}
            </button>
          </view>
      </view>
    </view>
  </view>
  <view v-if="showGroupDialog" class="group-modal" @tap="closeGroupDialog">
    <view class="group-panel glass-card" @tap.stop="">
      <view class="group-header">
        <text class="group-title">创建群聊</text>
        <button class="group-close" @tap="closeGroupDialog">关闭</button>
      </view>
      <input class="group-input" v-model="groupName" placeholder="请输入群聊名称" />
      <textarea
        class="group-textarea"
        v-model="groupMessage"
        placeholder="首条消息（可选）"
        auto-height
      />
      <scroll-view scroll-y class="group-list" :show-scrollbar="false">
        <view
          v-for="contact in contacts"
          :key="contact.username"
          :class="['group-item', { selected: selectedMembers.includes(contact.username) }]"
          @tap="toggleMember(contact.username)"
        >
          <text class="group-name">{{ contact.displayName }}</text>
          <text class="group-username">{{ contact.username }}</text>
          <text v-if="contact.remark" class="group-remark">{{ contact.remark }}</text>
        </view>
      </scroll-view>
      <view class="group-actions">
        <button
          class="group-btn"
          :disabled="!groupName || !groupName.trim() || !selectedMembers.length"
          @tap="confirmGroupSession"
        >
          创建
        </button>
      </view>
    </view>
  </view>
</view>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import SoftwarePicker from '@/components/SoftwarePicker.vue';
import { useAppStore } from '@/stores/app';
import { downloadTextFile } from '@/utils/download';

const appStore = useAppStore();
const { chatSessions, chatContacts, selectedSoftware } = storeToRefs(appStore);

const sessions = computed(() => chatSessions.value);
const contacts = computed(() => chatContacts.value);
const loadingSessions = computed(() => appStore.loading.chat);
const loadingMessages = computed(() => appStore.loading.chatMessages);
const activeSessionId = ref('');
const draft = ref('');
const sending = ref(false);
const lastMessageId = ref('');

const activeSession = computed(() => sessions.value.find((item) => item.id === activeSessionId.value) ?? null);
const emojiList = ['😀', '😁', '😂', '🤣', '😊', '😍', '🤔', '👍', '🎉', '🔥', '❤️', '🚀', '🙏', '😎', '🥳'];
const emojiPanelVisible = ref(false);
const sendingImage = ref(false);
const showGroupDialog = ref(false);
const groupName = ref('');
const groupMessage = ref('');
const selectedMembers = ref<string[]>([]);

onMounted(async () => {
  await appStore.ensureReady();
  await appStore.loadChatContacts();
  await appStore.loadChatSessions();
  ensureActiveSession();
});

watch(sessions, () => {
  ensureActiveSession();
  updateLastMessage();
});

watch(
  () => selectedSoftware.value,
  async (next, prev) => {
    if (!next || next === prev) return;
    activeSessionId.value = '';
    await appStore.loadChatContacts(true);
    await appStore.loadChatSessions();
    ensureActiveSession();
  }
);

watch(activeSessionId, async (next) => {
  if (!next) return;
  await loadMessages(next);
});

async function loadMessages(conversationId: string) {
  await appStore.loadChatMessages(conversationId);
  updateLastMessage();
}

function ensureActiveSession() {
  if (activeSessionId.value && sessions.value.some((item) => item.id === activeSessionId.value)) {
    return;
  }
  activeSessionId.value = sessions.value[0]?.id ?? '';
}

function updateLastMessage() {
  nextTick(() => {
    const target = activeSession.value;
    if (!target || !target.messages.length) {
      lastMessageId.value = '';
      return;
    }
    lastMessageId.value = target.messages[target.messages.length - 1].id;
  });
}

async function selectSession(id: string) {
  if (id === activeSessionId.value) return;
  activeSessionId.value = id;
  emojiPanelVisible.value = false;
}

async function startDirectSession() {
  await appStore.ensureReady();
  if (!selectedSoftware.value) {
    uni.showToast({ title: '请先选择软件位', icon: 'none' });
    return;
  }
  const list = contacts.value.length ? contacts.value : await appStore.loadChatContacts(true);
  if (!list || !list.length) {
    uni.showToast({ title: '暂无可用联系人', icon: 'none' });
    return;
  }

  try {
    const actionResult = await new Promise<UniApp.ShowActionSheetSuccessCallbackResult>((resolve, reject) => {
      uni.showActionSheet({
        itemList: list.map((item) => `${item.displayName}${item.displayName !== item.username ? `（${item.username}）` : ''}`),
        success: resolve,
        fail: reject
      });
    });

    const target = list[actionResult.tapIndex];
    if (!target) return;

    const messageResult = await new Promise<UniApp.ShowModalRes>((resolve, reject) => {
      uni.showModal({
        title: '发送首条消息',
        editable: true,
        placeholderText: '输入要发送的内容',
        confirmText: '发送',
        cancelText: '取消',
        success: resolve,
        fail: reject
      });
    });

    if (!messageResult.confirm || !messageResult.content || !messageResult.content.trim()) {
      return;
    }

    uni.showLoading({ title: '创建中...', mask: true });
    const conversation = await appStore.createDirectConversation(target.username, messageResult.content.trim());
    activeSessionId.value = conversation.id;
    await loadMessages(conversation.id);
    emojiPanelVisible.value = false;
    uni.showToast({ title: '会话已创建', icon: 'success' });
  } catch (error) {
    if ((error as UniApp.GeneralCallbackResult)?.errMsg?.includes('cancel')) {
      return;
    }
    console.error('startDirectSession error', error);
    uni.showToast({ title: '创建会话失败', icon: 'none' });
  } finally {
    uni.hideLoading();
  }
}

async function openGroupDialog() {
  await appStore.ensureReady();
  if (!selectedSoftware.value) {
    uni.showToast({ title: '请先选择软件位', icon: 'none' });
    return;
  }
  const list = contacts.value.length ? contacts.value : await appStore.loadChatContacts(true);
  if (!list || !list.length) {
    uni.showToast({ title: '暂无可用联系人', icon: 'none' });
    return;
  }
  groupName.value = '';
  groupMessage.value = '';
  selectedMembers.value = [];
  emojiPanelVisible.value = false;
  showGroupDialog.value = true;
}

function closeGroupDialog() {
  showGroupDialog.value = false;
  emojiPanelVisible.value = false;
}

function toggleMember(username: string) {
  if (!username) return;
  const list = [...selectedMembers.value];
  const index = list.indexOf(username);
  if (index >= 0) {
    list.splice(index, 1);
  } else {
    list.push(username);
  }
  selectedMembers.value = list;
}

async function confirmGroupSession() {
  const name = groupName.value.trim();
  if (!name) {
    uni.showToast({ title: '请输入群聊名称', icon: 'none' });
    return;
  }
  if (!selectedMembers.value.length) {
    uni.showToast({ title: '请选择成员', icon: 'none' });
    return;
  }

  try {
    uni.showLoading({ title: '创建中...', mask: true });
    const initial = groupMessage.value.trim();
    const conversation = await appStore.createGroupConversation(
      name,
      selectedMembers.value,
      initial ? { content: initial } : undefined
    );
    showGroupDialog.value = false;
    groupName.value = '';
    groupMessage.value = '';
    selectedMembers.value = [];
    emojiPanelVisible.value = false;
    activeSessionId.value = conversation.id;
    await loadMessages(conversation.id);
    uni.showToast({ title: '群聊已创建', icon: 'success' });
  } catch (error) {
    console.error('confirmGroupSession error', error);
    uni.showToast({ title: '创建失败', icon: 'none' });
  } finally {
    uni.hideLoading();
  }
}

async function exportHistory() {
  const session = activeSession.value;
  if (!session) {
    uni.showToast({ title: '请先选择会话', icon: 'none' });
    return;
  }

  try {
    uni.showLoading({ title: '导出中...', mask: true });
    const result = await appStore.exportChatHistory(session.id);
    downloadTextFile(result.filename, result.content);
    uni.showToast({ title: '导出完成', icon: 'success' });
  } catch (error) {
    console.error('exportHistory error', error);
    uni.showToast({ title: '导出失败', icon: 'none' });
  } finally {
    uni.hideLoading();
  }
}

async function sendMessage() {
  const session = activeSession.value;
  if (!session || !draft.value.trim()) {
    return;
  }

  sending.value = true;
  const content = draft.value.trim();

  try {
    await appStore.sendChatMessage(session.id, content);
    draft.value = '';
    emojiPanelVisible.value = false;
    await loadMessages(session.id);
  } finally {
    sending.value = false;
  }
}

function toggleEmojiPanel() {
  emojiPanelVisible.value = !emojiPanelVisible.value;
}

function insertEmoji(emoji: string) {
  draft.value += emoji;
}

function copyContent(value?: string) {
  const text = (value ?? '').toString().trim();
  if (!text) {
    uni.showToast({ title: '无可复制内容', icon: 'none' });
    return;
  }
  uni.setClipboardData({
    data: text,
    success: () => {
      uni.showToast({ title: '已复制', icon: 'success', duration: 800 });
    },
    fail: () => {
      uni.showToast({ title: '复制失败', icon: 'none' });
    }
  });
}

function previewImage(src: string) {
  if (!src) return;
  uni.previewImage({ urls: [src] });
}

async function readImageAsBase64(
  result: UniApp.ChooseImageSuccessCallbackResult,
  path: string
): Promise<{ base64: string; fileName: string }> {
  const tempFile = (result.tempFiles && result.tempFiles[0]) as any;
  if (tempFile && tempFile.file) {
    const file = tempFile.file as File;
    const dataUrl: string = await new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve((reader.result as string) || '');
      reader.onerror = () => reject(new Error('读取文件失败'));
      reader.readAsDataURL(file);
    });
    const base64 = dataUrl.includes(',') ? dataUrl.split(',')[1] ?? '' : dataUrl;
    return { base64, fileName: file.name || 'image.jpg' };
  }

  if (typeof uni.getFileSystemManager === 'function') {
    try {
      const fs = uni.getFileSystemManager();
      const base64 = await new Promise<string>((resolve, reject) => {
        fs.readFile({
          filePath: path,
          encoding: 'base64',
          success: (res) => resolve(res.data as string),
          fail: reject
        });
      });
      return { base64, fileName: path.split('/').pop() || 'image.jpg' };
    } catch (error) {
      console.error('readFileSystem error', error);
    }
  }

  const base64 = await new Promise<string>((resolve, reject) => {
    uni.request({
      url: path,
      method: 'GET',
      responseType: 'arraybuffer',
      success: (res) => {
        resolve(uni.arrayBufferToBase64(res.data as ArrayBuffer));
      },
      fail: reject
    });
  });
  return { base64, fileName: path.split('/').pop() || 'image.jpg' };
}

async function chooseImage() {
  const session = activeSession.value;
  if (!session) {
    uni.showToast({ title: '请先选择会话', icon: 'none' });
    return;
  }
  if (sendingImage.value) {
    return;
  }

  try {
    const result = await new Promise<UniApp.ChooseImageSuccessCallbackResult>((resolve, reject) => {
      uni.chooseImage({
        count: 1,
        sizeType: ['compressed', 'original'],
        success: resolve,
        fail: reject
      });
    });

    if (!result.tempFilePaths || !result.tempFilePaths.length) {
      return;
    }

    sendingImage.value = true;
    const path = result.tempFilePaths[0];
    const { base64, fileName } = await readImageAsBase64(result, path);
    const caption = draft.value.trim();

    await appStore.sendChatMessage(session.id, caption, {
      type: 'image',
      mediaBase64: base64,
      mediaName: fileName,
      caption
    });

    draft.value = '';
    emojiPanelVisible.value = false;
    await loadMessages(session.id);
    uni.showToast({ title: '图片已发送', icon: 'success' });
  } catch (error) {
    if ((error as UniApp.GeneralCallbackResult)?.errMsg?.includes('cancel')) {
      return;
    }
    console.error('chooseImage error', error);
    uni.showToast({ title: '发送失败', icon: 'none' });
  } finally {
    sendingImage.value = false;
  }
}
</script>

<style scoped lang="scss">
.page {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 40rpx 28rpx 80rpx;
}

.header {
  padding: 32rpx;
  display: flex;
  flex-direction: column;
  gap: 18rpx;

  @media (min-width: 768px) {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
  }
}

.title {
  font-size: 42rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.subtitle {
  font-size: 26rpx;
  color: var(--text-muted);
}

.header-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx;
  align-items: center;
}

.header-btn {
  padding: 16rpx 30rpx;
  border-radius: 999rpx;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.85), rgba(99, 102, 241, 0.8));
  color: #05070f;
  font-size: 26rpx;
  font-weight: 600;
  border: none;
}

.header-btn.ghost {
  background: transparent;
  border: 1rpx solid rgba(148, 163, 184, 0.4);
  color: var(--text-primary);
}

.header-btn.danger {
  background: transparent;
  border: 1rpx solid rgba(239, 68, 68, 0.6);
  color: rgba(248, 113, 113, 0.92);
}

.header-btn:disabled {
  opacity: 0.65;
}

.chat-layout {
  display: flex;
  flex-direction: column;
  gap: 24rpx;

  @media (min-width: 1024px) {
    flex-direction: row;
    align-items: stretch;
  }
}

.session-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  padding: 24rpx;
}

.session-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.session-title {
  font-size: 30rpx;
  font-weight: 600;
}

.session-counter {
  font-size: 22rpx;
  color: var(--text-muted);
}

.session-empty {
  padding: 40rpx 12rpx;
  text-align: center;
  color: var(--text-muted);
}

.session-list {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  max-height: 520rpx;
}

.session-item {
  padding: 16rpx 20rpx;
  border-radius: 16rpx;
  background: var(--surface-light);
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.session-item.active {
  border: 1rpx solid rgba(56, 189, 248, 0.45);
  background: rgba(15, 23, 42, 0.65);
}

.session-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12rpx;
}

.session-name {
  font-size: 26rpx;
  font-weight: 600;
}

.session-preview {
  font-size: 22rpx;
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
}

.session-time {
  font-size: 20rpx;
  color: var(--text-muted);
}

.session-unread {
  min-width: 28rpx;
  padding: 4rpx 10rpx;
  border-radius: 999rpx;
  background: rgba(56, 189, 248, 0.18);
  color: #38bdf8;
  font-size: 20rpx;
  text-align: center;
}

.chat-panel {
  flex: 2;
  display: flex;
  flex-direction: column;
  padding: 28rpx;
}

.chat-empty {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(148, 163, 184, 0.72);
}

.chat-body {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  height: 100%;
}

.chat-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.chat-title {
  font-size: 32rpx;
  font-weight: 600;
}

.chat-subtitle {
  font-size: 24rpx;
  color: var(--text-muted);
}

.chat-action {
  padding: 10rpx 18rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(56, 189, 248, 0.45);
  background: transparent;
  color: var(--text-muted);
  font-size: 24rpx;
}

.message-list {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 16rpx;
  padding: 12rpx 6rpx;
  max-height: 520rpx;
}

.message {
  align-self: flex-start;
  max-width: 85%;
  display: flex;
  flex-direction: column;
  gap: 10rpx;
  padding: 16rpx 20rpx;
  border-radius: 16rpx;
  background: var(--surface-light);
}

.message.system {
  background: rgba(30, 41, 59, 0.65);
}

.message.self {
  align-self: flex-end;
  background: rgba(56, 189, 248, 0.18);
}

.message-content {
  font-size: 26rpx;
  color: var(--text-primary);
  line-height: 1.6;
}

.message-image {
  width: 100%;
  max-width: 520rpx;
  border-radius: 14rpx;
  overflow: hidden;
  border: 1rpx solid rgba(56, 189, 248, 0.25);
}

.message.self .message-image {
  align-self: flex-end;
}

.message-caption {
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.92);
  line-height: 1.5;
}

.message-time {
  font-size: 20rpx;
  color: rgba(148, 163, 184, 0.65);
  align-self: flex-end;
}

.message-loading {
  align-self: center;
  font-size: 22rpx;
  color: var(--text-muted);
}

.composer {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 16rpx;
  padding: 18rpx 20rpx;
  border-radius: 22rpx;
}

.composer-toolbar {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.tool-btn {
  padding: 12rpx 22rpx;
  border-radius: 12rpx;
  background: rgba(15, 23, 42, 0.5);
  border: 1rpx solid rgba(59, 130, 246, 0.35);
  color: var(--text-primary);
  font-size: 26rpx;
}

.tool-btn:disabled {
  opacity: 0.5;
}

.composer-input {
  width: 100%;
  min-height: 140rpx;
  font-size: 26rpx;
  color: var(--text-primary);
  padding: 16rpx 18rpx;
  border-radius: 16rpx;
  background: var(--surface-light);
  border: 1rpx solid rgba(59, 130, 246, 0.2);
}

.composer-send {
  align-self: flex-end;
  padding: 16rpx 36rpx;
  border-radius: 999rpx;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.85), rgba(56, 189, 248, 0.85));
  color: #05070f;
  font-size: 26rpx;
  font-weight: 600;
  border: none;
}

.composer-send:disabled {
  opacity: 0.5;
}

.emoji-panel {
  position: absolute;
  left: 20rpx;
  right: 20rpx;
  bottom: 120rpx;
  padding: 18rpx 16rpx;
  border-radius: 18rpx;
  background: rgba(15, 23, 42, 0.95);
  border: 1rpx solid rgba(56, 189, 248, 0.35);
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(60rpx, 1fr));
  gap: 12rpx;
  z-index: 5;
}

.emoji-item {
  text-align: center;
  font-size: 32rpx;
  line-height: 1.6;
}

.group-modal {
  position: fixed;
  inset: 0;
  background: rgba(2, 6, 23, 0.72);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40rpx 28rpx;
  z-index: 20;
}

.group-panel {
  width: 100%;
  max-width: 640rpx;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  padding: 28rpx;
}

.group-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.group-title {
  font-size: 32rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.group-close {
  padding: 10rpx 24rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.35);
  background: transparent;
  color: var(--text-primary);
  font-size: 24rpx;
}

.group-input,
.group-textarea {
  width: 100%;
  padding: 16rpx 18rpx;
  border-radius: 16rpx;
  background: rgba(15, 23, 42, 0.5);
  border: 1rpx solid rgba(59, 130, 246, 0.2);
  color: var(--text-primary);
  font-size: 26rpx;
}

.group-textarea {
  min-height: 140rpx;
}

.group-list {
  flex: 1;
  max-height: 360rpx;
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.group-item {
  padding: 16rpx 20rpx;
  border-radius: 16rpx;
  background: var(--surface-light);
  border: 1rpx solid transparent;
  display: flex;
  flex-direction: column;
  gap: 6rpx;
}

.group-item.selected {
  border-color: rgba(56, 189, 248, 0.45);
  background: rgba(15, 23, 42, 0.65);
}

.group-name {
  font-size: 28rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.group-username {
  font-size: 24rpx;
  color: var(--text-muted);
}

.group-remark {
  font-size: 22rpx;
  color: rgba(148, 163, 184, 0.65);
}

.group-actions {
  display: flex;
  justify-content: flex-end;
}

.group-btn {
  padding: 16rpx 36rpx;
  border-radius: 999rpx;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.9), rgba(99, 102, 241, 0.85));
  color: #05070f;
  font-size: 26rpx;
  font-weight: 600;
  border: none;
}

.group-btn:disabled {
  opacity: 0.5;
}
</style>