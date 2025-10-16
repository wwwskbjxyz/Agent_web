-- Lanzou 下载链接表结构示例（SProtectAgentWeb.Api 使用）
-- 默认情况下，所有软件共用 lanzou_links 表。
-- 若按软件区分存储，请在表名后追加软件缩写，例如：lanzou_links_xw。
-- 缩写由系统根据软件名称生成：
--   * 中文会自动转为拼音首字母（例如“微信” -> wx，映射到 lanzou_links_wx）。
--   * 英文、数字会被转为小写并保留下划线（例如“MQTT” -> mqtt，映射到 lanzou_links_mqtt）。
--   * 其它字符会被替换成下划线并折叠重复下划线。
-- 可通过调用 SProtectAgentWeb.Api 的生成逻辑（LanzouLinkService.GenerateSlug）来验证结果。

CREATE TABLE IF NOT EXISTS `lanzou_links` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `链接` TEXT NOT NULL,
  `提取码` VARCHAR(64) NULL,
  `创建时间` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 克隆默认表结构以支持特定软件：
--   软件“炫舞” -> lanzou_links_xw
CREATE TABLE IF NOT EXISTS `lanzou_links_xw` LIKE `lanzou_links`;
--   软件“微信” -> lanzou_links_wx
CREATE TABLE IF NOT EXISTS `lanzou_links_wx` LIKE `lanzou_links`;
--   软件“MQTT” -> lanzou_links_mqtt
CREATE TABLE IF NOT EXISTS `lanzou_links_mqtt` LIKE `lanzou_links`;
--   软件“测试” -> lanzou_links_cs
CREATE TABLE IF NOT EXISTS `lanzou_links_cs` LIKE `lanzou_links`;
--   软件“畜生” 同样映射到 lanzou_links_cs（均为首字母 c 与 s）
--   （无需重复创建，两个软件共用同一张表。）

-- 插入一条下载链接示例数据
INSERT INTO `lanzou_links_xw` (`链接`, `提取码`, `创建时间`)
VALUES ('https://www.lanzou.com/xxxxxx', 'abcd', NOW());
INSERT INTO `lanzou_links_wx` (`链接`, `提取码`, `创建时间`)
VALUES ('https://www.lanzou.com/wx-demo', 'wx01', NOW());
INSERT INTO `lanzou_links_mqtt` (`链接`, `提取码`, `创建时间`)
VALUES ('https://www.lanzou.com/mqtt-demo', 'mqtt', NOW());
INSERT INTO `lanzou_links_cs` (`链接`, `提取码`, `创建时间`)
VALUES ('https://www.lanzou.com/cs-demo', 'ceshi', NOW());
INSERT INTO `lanzou_links_cs` (`链接`, `提取码`, `创建时间`)
VALUES ('https://www.lanzou.com/chusheng-demo', 'chusheng', NOW());

-- 如果希望所有软件共用同一组链接，可直接往 lanzou_links 表写入：
-- INSERT INTO `lanzou_links` (`链接`, `提取码`, `创建时间`)
-- VALUES ('https://www.lanzou.com/yyyyyy', 'efgh', NOW());
