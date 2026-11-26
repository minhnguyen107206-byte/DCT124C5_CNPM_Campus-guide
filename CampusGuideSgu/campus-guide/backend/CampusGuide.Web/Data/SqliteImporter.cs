using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CampusGuide.Web.Data;

//Chép toàn bộ dữ liệu từ file SQLite cũ (campusguide.db) sang SQL Server.
//Chỉ chạy khi SQL Server còn trống, giữ nguyên mã (Id) của mọi dòng.
public static class SqliteImporter {
    public static async Task<bool> RunAsync(AppDbContext dst, string sqlitePath) {
        if (!File.Exists(sqlitePath)) return false;
        if (await dst.VaiTros.AnyAsync()) return false;

        var opt = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={sqlitePath};Mode=ReadOnly")
            .Options;
        await using var src = new AppDbContext(opt);

        //Giữ một kết nối mở để SET IDENTITY_INSERT có tác dụng
        await dst.Database.OpenConnectionAsync();
        await using var tx = await dst.Database.BeginTransactionAsync();
        try {
            //Bảng cha trước, bảng con sau
            await Copy<VaiTro>(src, dst);
            await Copy<NguoiDung>(src, dst);
            await Copy<LoaiDiem>(src, dst);
            await Copy<NgonNgu>(src, dst);
            await Copy<GiongDoc>(src, dst);
            await Copy<Diem>(src, dst);
            await Copy<NoiDung>(src, dst);
            await Copy<Audio>(src, dst);
            await Copy<ThuatNgu>(src, dst);
            await Copy<BanDichThuatNgu>(src, dst);
            await Copy<Tour>(src, dst);
            await Copy<TourDiem>(src, dst);
            await Copy<GoiOffline>(src, dst);
            await Copy<ThongBao>(src, dst);
            await Copy<BanDichThongBao>(src, dst);
            await Copy<ThietBi>(src, dst);
            await Copy<LuotNghe>(src, dst);
            await Copy<LuotTruyCap>(src, dst);
            await Copy<DanhGia>(src, dst);
            await Copy<Video>(src, dst);
            await tx.CommitAsync();
        } catch {
            await tx.RollbackAsync();
            throw;
        } finally {
            await dst.Database.CloseConnectionAsync();
        }
        return true;
    }

    private static async Task Copy<T>(AppDbContext src, AppDbContext dst) where T : class {
        var rows = await src.Set<T>().AsNoTracking().ToListAsync();
        if (rows.Count == 0) return;

        var et = dst.Model.FindEntityType(typeof(T))!;
        var table = et.GetTableName()!;
        //Bảng có khoá tự tăng thì phải bật IDENTITY_INSERT mới chèn được mã cũ
        var identity = et.FindPrimaryKey()!.Properties.Any(p =>
            p.ValueGenerated == ValueGenerated.OnAdd && (p.ClrType == typeof(int) || p.ClrType == typeof(long)));

        if (identity) await dst.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + table + "] ON");
        dst.Set<T>().AddRange(rows);
        await dst.SaveChangesAsync();
        dst.ChangeTracker.Clear();
        if (identity) await dst.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + table + "] OFF");
        Console.WriteLine($"[SQL Server]   {table}: {rows.Count} dòng");
    }
}