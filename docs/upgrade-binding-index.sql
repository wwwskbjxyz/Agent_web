-- 手动修复 Bindings 表唯一索引的脚本
-- 请在执行前备份数据库。
-- 若以下外键不存在，可忽略报错继续执行
ALTER TABLE `Bindings` DROP FOREIGN KEY `FK_Bindings_Agents`;
ALTER TABLE `Bindings` DROP FOREIGN KEY `FK_Bindings_AuthorSoftwares`;
ALTER TABLE `Bindings` DROP INDEX `UX_Bindings`;
ALTER TABLE `Bindings` ADD UNIQUE INDEX `UX_Bindings` (`AgentId`,`AuthorSoftwareId`,`SoftwareCode`);
ALTER TABLE `Bindings` ADD CONSTRAINT `FK_Bindings_Agents` FOREIGN KEY (`AgentId`) REFERENCES `Agents`(`Id`) ON DELETE CASCADE;
ALTER TABLE `Bindings` ADD CONSTRAINT `FK_Bindings_AuthorSoftwares` FOREIGN KEY (`AuthorSoftwareId`) REFERENCES `AuthorSoftwares`(`Id`) ON DELETE CASCADE;
