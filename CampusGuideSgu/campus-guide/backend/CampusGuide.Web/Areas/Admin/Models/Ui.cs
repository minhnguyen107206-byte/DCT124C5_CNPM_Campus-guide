using CampusGuide.Web.Models;
using Microsoft.AspNetCore.Html;

namespace CampusGuide.Web.Areas.Admin.Models;

public static class Ui
{
    public static HtmlString Badge(string status)
    {
        var (text, cls) = status switch
        {
            TrangThaiDiem.Nhap => ("Nháp", "secondary"),
            TrangThaiDiem.DaXuatBan => ("Đã xuất bản", "success"),
            TrangThaiDiem.DaAn => ("Đã ẩn", "dark"),
            TrangThaiNoiDung.Goc => ("Bản gốc", "primary"),
            TrangThaiNoiDung.ChoDuyet => ("Chờ duyệt", "warning text-dark"),
            TrangThaiNoiDung.DaDuyet => ("Đã duyệt", "success"),
            TrangThaiNoiDung.TuChoi => ("Từ chối", "danger"),
            TrangThaiNoiDung.CanCapNhat => ("Cần cập nhật", "info text-dark"),
            TrangThaiNoiDung.LoiDich => ("Lỗi dịch", "danger"),
            "HoatDong" => ("Hoạt động", "success"),
            "BiKhoa" => ("Bị khóa", "danger"),
            _ => (status, "light text-dark"),
        };
        return new HtmlString($"<span class=\"badge bg-{cls}\">{System.Net.WebUtility.HtmlEncode(text)}</span>");
    }
}

