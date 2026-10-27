# 2026-10-27 Content 权限收紧

## 授权与变更
用户确认按建议操作。生产 `C:\Users\Administrator\Desktop\git\web24.05\Content` 原先从父目录继承 Everyone 完全控制。变更前盘点 4,903 个项目，无受保护继承或显式 Everyone 条目。

完整 ACL 备份为 `C:\SzaipaData\backup\content-acl-20261027-2042\content-acl.txt`（`icacls /save /t`，成功，约 1.1 MB），根目录原始 SDDL 保存在同目录 `root-sddl.txt`。根目录关闭继承并授予 SYSTEM 和 Administrators 完全控制、`IIS AppPool\SzaipaProd` 修改权限；子项继承根目录权限。

## 验证
- 全部 4,903 项检查无 Everyone ACL 条目，读取无错误。
- 公开首页、新闻列表、`/Content/images/TEXT.png` 和其 WebP 变体均为 HTTP 200。后台上传未实际写入验证；本次未写生产数据库或上传测试文件。
- 本次为权限操作，无应用代码变动，未运行构建或测试。

## 回退注意
旧站 `.NET v4.5` 应用池不在新 ACL 内。切回旧站前先恢复原 ACL，并验证旧站图片与上传；不要仅切 IIS。备份和原 SDDL 保留在服务器，以下恢复命令尚未实测，应在确认回退时由管理员执行并核验：

```powershell
$parent = 'C:\Users\Administrator\Desktop\git\web24.05'
$root = Join-Path $parent 'Content'
$backup = 'C:\SzaipaData\backup\content-acl-20261027-2042'
icacls $parent /restore (Join-Path $backup 'content-acl.txt') /c
$acl = Get-Acl -LiteralPath $root
$acl.SetSecurityDescriptorSddlForm([IO.File]::ReadAllText((Join-Path $backup 'root-sddl.txt')))
Set-Acl -LiteralPath $root -AclObject $acl
icacls $root
```
