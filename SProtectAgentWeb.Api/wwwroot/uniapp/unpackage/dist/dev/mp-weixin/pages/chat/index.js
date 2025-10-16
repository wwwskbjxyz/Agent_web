"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_app = require("../../stores/app.js");
const utils_download = require("../../utils/download.js");
if (!Math) {
  SoftwarePicker();
}
const SoftwarePicker = () => "../../components/SoftwarePicker.js";
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "index",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const { chatSessions, chatContacts, selectedSoftware } = common_vendor.storeToRefs(appStore);
    const sessions = common_vendor.computed(() => chatSessions.value);
    const contacts = common_vendor.computed(() => chatContacts.value);
    const loadingSessions = common_vendor.computed(() => appStore.loading.chat);
    const loadingMessages = common_vendor.computed(() => appStore.loading.chatMessages);
    const activeSessionId = common_vendor.ref("");
    const draft = common_vendor.ref("");
    const sending = common_vendor.ref(false);
    const lastMessageId = common_vendor.ref("");
    const activeSession = common_vendor.computed(() => sessions.value.find((item) => item.id === activeSessionId.value) ?? null);
    const emojiList = ["😀", "😁", "😂", "🤣", "😊", "😍", "🤔", "👍", "🎉", "🔥", "❤️", "🚀", "🙏", "😎", "🥳"];
    const emojiPanelVisible = common_vendor.ref(false);
    const sendingImage = common_vendor.ref(false);
    const showGroupDialog = common_vendor.ref(false);
    const groupName = common_vendor.ref("");
    const groupMessage = common_vendor.ref("");
    const selectedMembers = common_vendor.ref([]);
    common_vendor.onMounted(async () => {
      await appStore.ensureReady();
      await appStore.loadChatContacts();
      await appStore.loadChatSessions();
      ensureActiveSession();
    });
    common_vendor.watch(sessions, () => {
      ensureActiveSession();
      updateLastMessage();
    });
    common_vendor.watch(
      () => selectedSoftware.value,
      async (next, prev) => {
        if (!next || next === prev)
          return;
        activeSessionId.value = "";
        await appStore.loadChatContacts(true);
        await appStore.loadChatSessions();
        ensureActiveSession();
      }
    );
    common_vendor.watch(activeSessionId, async (next) => {
      if (!next)
        return;
      await loadMessages(next);
    });
    async function loadMessages(conversationId) {
      await appStore.loadChatMessages(conversationId);
      updateLastMessage();
    }
    function ensureActiveSession() {
      var _a;
      if (activeSessionId.value && sessions.value.some((item) => item.id === activeSessionId.value)) {
        return;
      }
      activeSessionId.value = ((_a = sessions.value[0]) == null ? void 0 : _a.id) ?? "";
    }
    function updateLastMessage() {
      common_vendor.nextTick$1(() => {
        const target = activeSession.value;
        if (!target || !target.messages.length) {
          lastMessageId.value = "";
          return;
        }
        lastMessageId.value = target.messages[target.messages.length - 1].id;
      });
    }
    async function selectSession(id) {
      if (id === activeSessionId.value)
        return;
      activeSessionId.value = id;
      emojiPanelVisible.value = false;
    }
    async function startDirectSession() {
      var _a;
      await appStore.ensureReady();
      if (!selectedSoftware.value) {
        common_vendor.index.showToast({ title: "请先选择软件位", icon: "none" });
        return;
      }
      const list = contacts.value.length ? contacts.value : await appStore.loadChatContacts(true);
      if (!list || !list.length) {
        common_vendor.index.showToast({ title: "暂无可用联系人", icon: "none" });
        return;
      }
      try {
        const actionResult = await new Promise((resolve, reject) => {
          common_vendor.index.showActionSheet({
            itemList: list.map((item) => `${item.displayName}${item.displayName !== item.username ? `（${item.username}）` : ""}`),
            success: resolve,
            fail: reject
          });
        });
        const target = list[actionResult.tapIndex];
        if (!target)
          return;
        const messageResult = await new Promise((resolve, reject) => {
          common_vendor.index.showModal({
            title: "发送首条消息",
            editable: true,
            placeholderText: "输入要发送的内容",
            confirmText: "发送",
            cancelText: "取消",
            success: resolve,
            fail: reject
          });
        });
        if (!messageResult.confirm || !messageResult.content || !messageResult.content.trim()) {
          return;
        }
        common_vendor.index.showLoading({ title: "创建中...", mask: true });
        const conversation = await appStore.createDirectConversation(target.username, messageResult.content.trim());
        activeSessionId.value = conversation.id;
        await loadMessages(conversation.id);
        emojiPanelVisible.value = false;
        common_vendor.index.showToast({ title: "会话已创建", icon: "success" });
      } catch (error) {
        if ((_a = error == null ? void 0 : error.errMsg) == null ? void 0 : _a.includes("cancel")) {
          return;
        }
        common_vendor.index.__f__("error", "at pages/chat/index.vue:283", "startDirectSession error", error);
        common_vendor.index.showToast({ title: "创建会话失败", icon: "none" });
      } finally {
        common_vendor.index.hideLoading();
      }
    }
    async function openGroupDialog() {
      await appStore.ensureReady();
      if (!selectedSoftware.value) {
        common_vendor.index.showToast({ title: "请先选择软件位", icon: "none" });
        return;
      }
      const list = contacts.value.length ? contacts.value : await appStore.loadChatContacts(true);
      if (!list || !list.length) {
        common_vendor.index.showToast({ title: "暂无可用联系人", icon: "none" });
        return;
      }
      groupName.value = "";
      groupMessage.value = "";
      selectedMembers.value = [];
      emojiPanelVisible.value = false;
      showGroupDialog.value = true;
    }
    function closeGroupDialog() {
      showGroupDialog.value = false;
      emojiPanelVisible.value = false;
    }
    function toggleMember(username) {
      if (!username)
        return;
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
        common_vendor.index.showToast({ title: "请输入群聊名称", icon: "none" });
        return;
      }
      if (!selectedMembers.value.length) {
        common_vendor.index.showToast({ title: "请选择成员", icon: "none" });
        return;
      }
      try {
        common_vendor.index.showLoading({ title: "创建中...", mask: true });
        const initial = groupMessage.value.trim();
        const conversation = await appStore.createGroupConversation(
          name,
          selectedMembers.value,
          initial ? { content: initial } : void 0
        );
        showGroupDialog.value = false;
        groupName.value = "";
        groupMessage.value = "";
        selectedMembers.value = [];
        emojiPanelVisible.value = false;
        activeSessionId.value = conversation.id;
        await loadMessages(conversation.id);
        common_vendor.index.showToast({ title: "群聊已创建", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/chat/index.vue:353", "confirmGroupSession error", error);
        common_vendor.index.showToast({ title: "创建失败", icon: "none" });
      } finally {
        common_vendor.index.hideLoading();
      }
    }
    async function exportHistory() {
      const session = activeSession.value;
      if (!session) {
        common_vendor.index.showToast({ title: "请先选择会话", icon: "none" });
        return;
      }
      try {
        common_vendor.index.showLoading({ title: "导出中...", mask: true });
        const result = await appStore.exportChatHistory(session.id);
        utils_download.downloadTextFile(result.filename, result.content);
        common_vendor.index.showToast({ title: "导出完成", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/chat/index.vue:373", "exportHistory error", error);
        common_vendor.index.showToast({ title: "导出失败", icon: "none" });
      } finally {
        common_vendor.index.hideLoading();
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
        draft.value = "";
        emojiPanelVisible.value = false;
        await loadMessages(session.id);
      } finally {
        sending.value = false;
      }
    }
    function toggleEmojiPanel() {
      emojiPanelVisible.value = !emojiPanelVisible.value;
    }
    function insertEmoji(emoji) {
      draft.value += emoji;
    }
    function copyContent(value) {
      const text = (value ?? "").toString().trim();
      if (!text) {
        common_vendor.index.showToast({ title: "无可复制内容", icon: "none" });
        return;
      }
      common_vendor.index.setClipboardData({
        data: text,
        success: () => {
          common_vendor.index.showToast({ title: "已复制", icon: "success", duration: 800 });
        },
        fail: () => {
          common_vendor.index.showToast({ title: "复制失败", icon: "none" });
        }
      });
    }
    function previewImage(src) {
      if (!src)
        return;
      common_vendor.index.previewImage({ urls: [src] });
    }
    async function readImageAsBase64(result, path) {
      const tempFile = result.tempFiles && result.tempFiles[0];
      if (tempFile && tempFile.file) {
        const file = tempFile.file;
        const dataUrl = await new Promise((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () => resolve(reader.result || "");
          reader.onerror = () => reject(new Error("读取文件失败"));
          reader.readAsDataURL(file);
        });
        const base642 = dataUrl.includes(",") ? dataUrl.split(",")[1] ?? "" : dataUrl;
        return { base64: base642, fileName: file.name || "image.jpg" };
      }
      if (typeof common_vendor.index.getFileSystemManager === "function") {
        try {
          const fs = common_vendor.index.getFileSystemManager();
          const base642 = await new Promise((resolve, reject) => {
            fs.readFile({
              filePath: path,
              encoding: "base64",
              success: (res) => resolve(res.data),
              fail: reject
            });
          });
          return { base64: base642, fileName: path.split("/").pop() || "image.jpg" };
        } catch (error) {
          common_vendor.index.__f__("error", "at pages/chat/index.vue:459", "readFileSystem error", error);
        }
      }
      const base64 = await new Promise((resolve, reject) => {
        common_vendor.index.request({
          url: path,
          method: "GET",
          responseType: "arraybuffer",
          success: (res) => {
            resolve(common_vendor.index.arrayBufferToBase64(res.data));
          },
          fail: reject
        });
      });
      return { base64, fileName: path.split("/").pop() || "image.jpg" };
    }
    async function chooseImage() {
      var _a;
      const session = activeSession.value;
      if (!session) {
        common_vendor.index.showToast({ title: "请先选择会话", icon: "none" });
        return;
      }
      if (sendingImage.value) {
        return;
      }
      try {
        const result = await new Promise((resolve, reject) => {
          common_vendor.index.chooseImage({
            count: 1,
            sizeType: ["compressed", "original"],
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
          type: "image",
          mediaBase64: base64,
          mediaName: fileName,
          caption
        });
        draft.value = "";
        emojiPanelVisible.value = false;
        await loadMessages(session.id);
        common_vendor.index.showToast({ title: "图片已发送", icon: "success" });
      } catch (error) {
        if ((_a = error == null ? void 0 : error.errMsg) == null ? void 0 : _a.includes("cancel")) {
          return;
        }
        common_vendor.index.__f__("error", "at pages/chat/index.vue:521", "chooseImage error", error);
        common_vendor.index.showToast({ title: "发送失败", icon: "none" });
      } finally {
        sendingImage.value = false;
      }
    }
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.o(startDirectSession),
        b: common_vendor.o(openGroupDialog),
        c: common_vendor.t(sessions.value.length),
        d: loadingSessions.value
      }, loadingSessions.value ? {} : !sessions.value.length ? {} : {
        f: common_vendor.f(sessions.value, (item, k0, i0) => {
          return common_vendor.e({
            a: common_vendor.t(item.title),
            b: item.unread
          }, item.unread ? {
            c: common_vendor.t(item.unread)
          } : {}, {
            d: common_vendor.t(item.preview || "暂未开始对话"),
            e: common_vendor.t(item.updatedAt),
            f: item.id,
            g: common_vendor.n({
              active: item.id === activeSessionId.value
            }),
            h: common_vendor.o(($event) => selectSession(item.id), item.id)
          });
        })
      }, {
        e: !sessions.value.length,
        g: !activeSession.value
      }, !activeSession.value ? {} : common_vendor.e({
        h: common_vendor.t(activeSession.value.title),
        i: common_vendor.t(activeSession.value.participants.join("、")),
        j: common_vendor.o(exportHistory),
        k: common_vendor.f(activeSession.value.messages, (message, k0, i0) => {
          return common_vendor.e({
            a: message.type === "image"
          }, message.type === "image" ? common_vendor.e({
            b: message.content,
            c: common_vendor.o(($event) => previewImage(message.content), message.id),
            d: message.caption
          }, message.caption ? {
            e: common_vendor.t(message.caption),
            f: common_vendor.o(($event) => copyContent(message.caption), message.id)
          } : {}) : {
            g: common_vendor.t(message.content),
            h: common_vendor.o(($event) => copyContent(message.content), message.id)
          }, {
            i: common_vendor.t(message.time),
            j: common_vendor.o(($event) => copyContent(message.time), message.id),
            k: message.id,
            l: message.id,
            m: common_vendor.n(message.sender === "user" ? "self" : "system")
          });
        }),
        l: loadingMessages.value
      }, loadingMessages.value ? {} : {}, {
        m: lastMessageId.value,
        n: common_vendor.o(toggleEmojiPanel),
        o: sending.value || sendingImage.value,
        p: common_vendor.o(chooseImage),
        q: draft.value,
        r: common_vendor.o(($event) => draft.value = $event.detail.value),
        s: emojiPanelVisible.value
      }, emojiPanelVisible.value ? {
        t: common_vendor.f(emojiList, (emoji, k0, i0) => {
          return {
            a: common_vendor.t(emoji),
            b: emoji,
            c: common_vendor.o(($event) => insertEmoji(emoji), emoji)
          };
        })
      } : {}, {
        v: common_vendor.t(sending.value ? "发送中..." : "发送消息"),
        w: !draft.value.trim() || sending.value || sendingImage.value,
        x: common_vendor.o(sendMessage)
      }), {
        y: showGroupDialog.value
      }, showGroupDialog.value ? {
        z: common_vendor.o(closeGroupDialog),
        A: groupName.value,
        B: common_vendor.o(($event) => groupName.value = $event.detail.value),
        C: groupMessage.value,
        D: common_vendor.o(($event) => groupMessage.value = $event.detail.value),
        E: common_vendor.f(contacts.value, (contact, k0, i0) => {
          return common_vendor.e({
            a: common_vendor.t(contact.displayName),
            b: common_vendor.t(contact.username),
            c: contact.remark
          }, contact.remark ? {
            d: common_vendor.t(contact.remark)
          } : {}, {
            e: contact.username,
            f: common_vendor.n({
              selected: selectedMembers.value.includes(contact.username)
            }),
            g: common_vendor.o(($event) => toggleMember(contact.username), contact.username)
          });
        }),
        F: !groupName.value || !groupName.value.trim() || !selectedMembers.value.length,
        G: common_vendor.o(confirmGroupSession),
        H: common_vendor.o(() => {
        }),
        I: common_vendor.o(closeGroupDialog)
      } : {});
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-5a559478"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/chat/index.js.map
