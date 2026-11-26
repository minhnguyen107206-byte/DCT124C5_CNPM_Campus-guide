using CampusGuide.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGuide.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<VaiTro> VaiTros => Set<VaiTro>();
    public DbSet<NguoiDung> NguoiDungs => Set<NguoiDung>();
    public DbSet<LoaiDiem> LoaiDiems => Set<LoaiDiem>();
    public DbSet<Diem> Diems => Set<Diem>();
    public DbSet<NgonNgu> NgonNgus => Set<NgonNgu>();
    public DbSet<GiongDoc> GiongDocs => Set<GiongDoc>();
    public DbSet<NoiDung> NoiDungs => Set<NoiDung>();
    public DbSet<Audio> Audios => Set<Audio>();
    public DbSet<ThuatNgu> ThuatNgus => Set<ThuatNgu>();
    public DbSet<BanDichThuatNgu> BanDichThuatNgus => Set<BanDichThuatNgu>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourDiem> TourDiems => Set<TourDiem>();
    public DbSet<GoiOffline> GoiOfflines => Set<GoiOffline>();
    public DbSet<ThongBao> ThongBaos => Set<ThongBao>();
    public DbSet<BanDichThongBao> BanDichThongBaos => Set<BanDichThongBao>();
    public DbSet<ThietBi> ThietBis => Set<ThietBi>();
    public DbSet<LuotNghe> LuotNghes => Set<LuotNghe>();
    public DbSet<LuotTruyCap> LuotTruyCaps => Set<LuotTruyCap>();
    public DbSet<DanhGia> DanhGias => Set<DanhGia>();
    public DbSet<Video> Videos => Set<Video>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<VaiTro>().ToTable("VaiTro");
        b.Entity<NguoiDung>(e =>
        {
            e.ToTable("NguoiDung");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.VaiTro).WithMany(v => v.NguoiDungs).HasForeignKey(x => x.MaVaiTro);
        });
        b.Entity<LoaiDiem>().ToTable("LoaiDiem");
        b.Entity<Diem>(e =>
        {
            e.ToTable("Diem");
            e.HasIndex(x => x.TenDiem).IsUnique();
            e.HasIndex(x => x.MaQR).IsUnique();
            e.HasOne(x => x.LoaiDiem).WithMany().HasForeignKey(x => x.MaLoai);
            e.HasOne(x => x.NguoiTao).WithMany().HasForeignKey(x => x.MaNguoiTao).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<NgonNgu>().ToTable("NgonNgu");
        b.Entity<GiongDoc>(e =>
        {
            e.ToTable("GiongDoc");
            e.HasOne(x => x.NgonNgu).WithMany(n => n.GiongDocs).HasForeignKey(x => x.MaNgonNgu);
        });
        b.Entity<NoiDung>(e =>
        {
            e.ToTable("NoiDung");
            e.HasIndex(x => new { x.MaDiem, x.MaNgonNgu }).IsUnique();
            e.HasOne(x => x.Diem).WithMany(d => d.NoiDungs).HasForeignKey(x => x.MaDiem);
            e.HasOne(x => x.NgonNgu).WithMany().HasForeignKey(x => x.MaNgonNgu);
            e.HasOne(x => x.NguoiDuyet).WithMany().HasForeignKey(x => x.MaNguoiDuyet).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Audio>(e =>
        {
            e.ToTable("Audio");
            e.HasOne(x => x.NoiDung).WithMany(n => n.Audios).HasForeignKey(x => x.MaNoiDung);
            e.HasOne(x => x.GiongDoc).WithMany().HasForeignKey(x => x.MaGiong).OnDelete(DeleteBehavior.ClientSetNull);   //SQL Server không cho 2 đường xoá dây chuyền tới Audio
        });
        b.Entity<ThuatNgu>(e =>
        {
            e.ToTable("ThuatNgu");
            e.HasIndex(x => x.TuGoc).IsUnique();
        });
        b.Entity<BanDichThuatNgu>(e =>
        {
            e.ToTable("BanDichThuatNgu");
            e.HasKey(x => new { x.MaThuatNgu, x.MaNgonNgu });
            e.HasOne(x => x.ThuatNgu).WithMany(t => t.BanDichs).HasForeignKey(x => x.MaThuatNgu);
            e.HasOne(x => x.NgonNgu).WithMany().HasForeignKey(x => x.MaNgonNgu);
        });
        b.Entity<Tour>().ToTable("Tour");
        b.Entity<TourDiem>(e =>
        {
            e.ToTable("Tour_Diem");
            e.HasKey(x => new { x.MaTour, x.MaDiem });
            e.HasOne(x => x.Tour).WithMany(t => t.Diems).HasForeignKey(x => x.MaTour);
            e.HasOne(x => x.Diem).WithMany().HasForeignKey(x => x.MaDiem);
        });
        b.Entity<GoiOffline>(e =>
        {
            e.ToTable("GoiOffline");
            e.HasOne(x => x.Tour).WithMany().HasForeignKey(x => x.MaTour);
            e.HasOne<NgonNgu>().WithMany().HasForeignKey(x => x.MaNgonNgu);
        });
        b.Entity<ThongBao>(e =>
        {
            e.ToTable("ThongBao");
            e.HasOne(x => x.NguoiTao).WithMany().HasForeignKey(x => x.MaNguoiTao).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<BanDichThongBao>(e =>
        {
            e.ToTable("BanDichThongBao");
            e.HasKey(x => new { x.MaThongBao, x.MaNgonNgu });
            e.HasOne(x => x.ThongBao).WithMany(t => t.BanDichs).HasForeignKey(x => x.MaThongBao);
            e.HasOne<NgonNgu>().WithMany().HasForeignKey(x => x.MaNgonNgu);
        });
        b.Entity<ThietBi>(e =>
        {
            e.ToTable("ThietBi");
            e.HasOne<NgonNgu>().WithMany().HasForeignKey(x => x.MaNgonNgu).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<LuotNghe>(e =>
        {
            e.ToTable("LuotNghe");
            e.HasIndex(x => x.ThoiGian);
            e.HasOne(x => x.ThietBi).WithMany().HasForeignKey(x => x.MaThietBi);
            e.HasOne(x => x.Diem).WithMany().HasForeignKey(x => x.MaDiem);
            e.HasOne<NgonNgu>().WithMany().HasForeignKey(x => x.MaNgonNgu);
        });
        b.Entity<LuotTruyCap>(e =>
        {
            e.ToTable("LuotTruyCap");
            e.HasIndex(x => x.BatDau);
            e.HasOne(x => x.Diem).WithMany().HasForeignKey(x => x.MaDiem).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<DanhGia>(e =>
        {
            e.ToTable("DanhGia");
            e.HasIndex(x => new { x.MaThietBi, x.MaDiem }).IsUnique();
            e.HasOne(x => x.ThietBi).WithMany().HasForeignKey(x => x.MaThietBi);
            e.HasOne(x => x.Diem).WithMany().HasForeignKey(x => x.MaDiem);
        });
        // ---- Khu người dùng (trang web công khai) ----
        b.Entity<Video>(e =>
        {
            e.ToTable("Video");
            e.HasOne(x => x.Diem).WithMany().HasForeignKey(x => x.MaDiem).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
