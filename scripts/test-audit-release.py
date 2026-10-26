"""发布边界回归：确保凭据/数据库误混入时失败，且诊断不泄露秘密。"""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("audit_release", Path(__file__).with_name("audit-release.py"))
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class ReleaseBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        for name in release.REQUIRED:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("placeholder", encoding="utf-8")
        (self.root / "appsettings.json").write_text('{"ConnectionStrings":{"Szaipa":""}}')
        (self.root / "web.config").write_text(
            '<configuration><system.webServer><aspNetCore processPath="dotnet" '
            'arguments=".\\Szaipa.Web.dll" /></system.webServer></configuration>')

    def tearDown(self):
        self.temp.cleanup()

    def test_accepts_empty_config_and_hashes_files(self):
        errors, files = release.audit(self.root)
        self.assertEqual([], errors)
        self.assertEqual(len(release.REQUIRED), len(files))
        before = next(row for row in files if row["path"] == "Szaipa.Web.dll")["sha256"]
        (self.root / "Szaipa.Web.dll").write_bytes(b"changed")
        _, changed = release.audit(self.root)
        after = next(row for row in changed if row["path"] == "Szaipa.Web.dll")["sha256"]
        self.assertNotEqual(before, after)

    def test_rejects_external_data_and_source_code(self):
        for name in [".local-db/szaipa.db", "App_Data/key.xml", "Content/photo.jpg",
                     "wwwroot/public/src/swiper-public.js", "appsettings.Local.json"]:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("{}")
        errors, _ = release.audit(self.root)
        self.assertTrue(all(any(name in error for error in errors) for name in [
            "szaipa.db", "key.xml", "photo.jpg", "swiper-public.js", "appsettings.Local.json"]))

    def test_rejects_connection_secret_without_echoing_it(self):
        secret = "server=private;password=never-print-this"
        (self.root / "appsettings.json").write_text(
            '{"ConnectionStrings":{"Szaipa":"' + secret + '"}}')
        errors, _ = release.audit(self.root)
        self.assertTrue(any("ConnectionStrings:Szaipa" in error for error in errors))
        self.assertNotIn(secret, "\n".join(errors))

    def test_rejects_debug_writes_and_wrong_iis_entry(self):
        (self.root / "appsettings.json").write_text('{"LocalMirror":{"Enabled":true}}')
        (self.root / "web.config").write_text('<configuration/>')
        errors, _ = release.audit(self.root)
        self.assertTrue(any("LocalMirror:Enabled" in error for error in errors))
        self.assertTrue(any("IIS" in error for error in errors))

    def test_rejects_callback_token_without_echoing_it(self):
        (self.root / "appsettings.json").write_text('{"WeChat":{"Callback":{"Token":"secret-callback-token"}}}')
        errors, _ = release.audit(self.root)
        self.assertTrue(any("WeChat:Callback:Token" in error for error in errors))
        self.assertNotIn("secret-callback-token", "\n".join(errors))

    def test_rejects_symlink_to_external_file(self):
        (self.root / "external.txt").symlink_to(self.root / "Szaipa.Web.dll")
        errors, _ = release.audit(self.root)
        self.assertTrue(any("符号链接" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
