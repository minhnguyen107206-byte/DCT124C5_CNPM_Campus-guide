using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Data;

/// <summary>EnsureCreated chỉ tạo bảng khi CSDL chưa có → bảng thêm sau (LuotTruyCap, Video) phải tự tạo ở đây
/// để máy đã có campusguide.db không phải xoá CSDL.</summary>
public static class SchemaUpdater
{
    public static async Task RunAsync(AppDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "LuotTruyCap" (
                    "MaLuot" INTEGER NOT NULL CONSTRAINT "PK_LuotTruyCap" PRIMARY KEY AUTOINCREMENT,
                    "MaPhien" TEXT NOT NULL,
                    "MaDiem" INTEGER NULL REFERENCES "Diem" ("MaDiem") ON DELETE SET NULL,
                    "MaNgonNgu" TEXT NULL,
                    "Nguon" TEXT NOT NULL,
                    "NenTang" TEXT NOT NULL,
                    "BatDau" TEXT NOT NULL,
                    "ThoiGianXem" INTEGER NOT NULL,
                    "CoNghe" INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_LuotTruyCap_BatDau" ON "LuotTruyCap" ("BatDau");
                CREATE INDEX IF NOT EXISTS "IX_LuotTruyCap_MaDiem" ON "LuotTruyCap" ("MaDiem");
                """);

            // Khu người dùng: video/trực tiếp
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Video" (
                    "MaVideo" INTEGER NOT NULL CONSTRAINT "PK_Video" PRIMARY KEY AUTOINCREMENT,
                    "TieuDe" TEXT NOT NULL,
                    "MoTa" TEXT NULL,
                    "DuongDan" TEXT NOT NULL,
                    "LaTrucTiep" INTEGER NOT NULL,
                    "MaDiem" INTEGER NULL,
                    "BatDau" TEXT NULL,
                    "HienThi" INTEGER NOT NULL,
                    "ThuTu" INTEGER NOT NULL,
                    "NgayTao" TEXT NOT NULL,
                    CONSTRAINT "FK_Video_Diem_MaDiem" FOREIGN KEY ("MaDiem") REFERENCES "Diem" ("MaDiem") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_Video_MaDiem" ON "Video" ("MaDiem");
                """);

            // Ràng buộc bán kính điểm 0–30 m: đưa dữ liệu cũ về trong khoảng, rồi dùng trigger chặn thêm/sửa sai
            // (SQLite không thêm được CHECK vào bảng đã có)
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "Diem" SET "BanKinh" = 30 WHERE "BanKinh" > 30;
                UPDATE "Diem" SET "BanKinh" = 0 WHERE "BanKinh" < 0;
                CREATE TRIGGER IF NOT EXISTS "CK_Diem_BanKinh_Them" BEFORE INSERT ON "Diem"
                WHEN NEW."BanKinh" < 0 OR NEW."BanKinh" > 30
                BEGIN SELECT RAISE(ABORT, 'Bán kính phải từ 0 đến 30 m'); END;
                CREATE TRIGGER IF NOT EXISTS "CK_Diem_BanKinh_Sua" BEFORE UPDATE OF "BanKinh" ON "Diem"
                WHEN NEW."BanKinh" < 0 OR NEW."BanKinh" > 30
                BEGIN SELECT RAISE(ABORT, 'Bán kính phải từ 0 đến 30 m'); END;
                """);

            // Xoá một điểm (kể cả xoá tay trong DB Browser) thì xoá luôn mọi dòng dính khoá MaDiem
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER IF NOT EXISTS "XoaDiem_XoaLienQuan" BEFORE DELETE ON "Diem"
                BEGIN
                    DELETE FROM "Audio" WHERE "MaNoiDung" IN (SELECT "MaNoiDung" FROM "NoiDung" WHERE "MaDiem" = OLD."MaDiem");
                    DELETE FROM "NoiDung" WHERE "MaDiem" = OLD."MaDiem";
                    DELETE FROM "Tour_Diem" WHERE "MaDiem" = OLD."MaDiem";
                    DELETE FROM "DanhGia" WHERE "MaDiem" = OLD."MaDiem";
                    DELETE FROM "LuotNghe" WHERE "MaDiem" = OLD."MaDiem";
                    DELETE FROM "LuotTruyCap" WHERE "MaDiem" = OLD."MaDiem";
                    DELETE FROM "Video" WHERE "MaDiem" = OLD."MaDiem";
                END;
                """);
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "LuotTruyCap" (
                    "MaLuot" BIGSERIAL PRIMARY KEY,
                    "MaPhien" VARCHAR(64) NOT NULL,
                    "MaDiem" INTEGER NULL REFERENCES "Diem" ("MaDiem") ON DELETE SET NULL,
                    "MaNgonNgu" VARCHAR(10) NULL,
                    "Nguon" VARCHAR(10) NOT NULL,
                    "NenTang" VARCHAR(40) NOT NULL,
                    "BatDau" TIMESTAMP WITH TIME ZONE NOT NULL,
                    "ThoiGianXem" INTEGER NOT NULL,
                    "CoNghe" BOOLEAN NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_LuotTruyCap_BatDau" ON "LuotTruyCap" ("BatDau");
                """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Video" (
                    "MaVideo" SERIAL PRIMARY KEY,
                    "TieuDe" VARCHAR(200) NOT NULL,
                    "MoTa" VARCHAR(1000) NULL,
                    "DuongDan" VARCHAR(500) NOT NULL,
                    "LaTrucTiep" BOOLEAN NOT NULL,
                    "MaDiem" INTEGER NULL REFERENCES "Diem" ("MaDiem") ON DELETE SET NULL,
                    "BatDau" TIMESTAMP NULL,
                    "HienThi" BOOLEAN NOT NULL,
                    "ThuTu" INTEGER NOT NULL,
                    "NgayTao" TIMESTAMP NOT NULL
                );
                """);

            // Ràng buộc bán kính điểm 0–30 m
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "Diem" SET "BanKinh" = LEAST(GREATEST("BanKinh", 0), 30);
                ALTER TABLE "Diem" DROP CONSTRAINT IF EXISTS "CK_Diem_BanKinh";
                ALTER TABLE "Diem" ADD CONSTRAINT "CK_Diem_BanKinh" CHECK ("BanKinh" BETWEEN 0 AND 30);
                """);
        }
    }
}
