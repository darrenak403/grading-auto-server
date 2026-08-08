using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GivenAPIs.Models
{
    // --- Các thực thể Entity mô phỏng theo cấu trúc Database Schema ---
    public class MembershipPackage
    {
        public int PackageID { get; set; }
        public string PackageName { get; set; } = null!;
        public decimal Price { get; set; }
    }

    public class Member
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = null!;
        public int PackageID { get; set; }
        public DateTime JoinDate { get; set; }
    }

    public class Trainer
    {
        public int TrainerID { get; set; }
        public string TrainerName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int ExperienceYears { get; set; }
    }

    public class Specialization
    {
        public int SpecID { get; set; }
        public string SpecName { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class Booking
    {
        public int BookingID { get; set; }
        public int TrainerID { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime SessionTime { get; set; }
    }

    public class BookingDetail
    {
        public int BookingID { get; set; }
        public int MemberID { get; set; }
        public int DurationMinutes { get; set; }
        public string Status { get; set; } = null!; // Da tap, Da huy, Cho mien
    }

    public class TrainerSpec
    {
        public int TrainerID { get; set; }
        public int SpecID { get; set; }
    }

    // --- Data Transfer Objects (DTOs) phục vụ truyền tải qua API ---
    public class MemberSearchDTO
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = null!;
        public DateTime JoinDate { get; set; }
        public string PackageName { get; set; } = null!;
    }

    public class MemberSessionDTO
    {
        public int BookingID { get; set; }
        public string TrainerName { get; set; } = null!;
        public DateTime SessionTime { get; set; }
        public int DurationMinutes { get; set; }
        public string Status { get; set; } = null!;
    }

    public class MemberDetailDTO
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = null!;
        public DateTime JoinDate { get; set; }
        public string PackageName { get; set; } = null!;
        public decimal PackagePrice { get; set; }
        public List<MemberSessionDTO> Sessions { get; set; } = new();
    }
}

namespace GivenAPIs.Data
{
    using GivenAPIs.Models;

    // --- Khởi tạo dữ liệu mẫu trong bộ nhớ (In-Memory) ---
    public static class DataInitializer
    {
        public static List<MembershipPackage> MembershipPackages = new()
        {
            new MembershipPackage { PackageID = 1, PackageName = "Goi Thang Co Ban", Price = 300000.00m },
            new MembershipPackage { PackageID = 2, PackageName = "Goi Binh Minh", Price = 450000.00m },
            new MembershipPackage { PackageID = 3, PackageName = "Goi Chuyen Nghiep", Price = 800000.00m },
            new MembershipPackage { PackageID = 4, PackageName = "Goi Gia Dinh", Price = 1500000.00m },
            new MembershipPackage { PackageID = 5, PackageName = "Goi VIP Tron Goi", Price = 3000000.00m }
        };

        public static List<Member> Members = new()
        {
            new Member { MemberID = 1, FullName = "Cao Van Nam", PackageID = 1, JoinDate = new DateTime(2026, 1, 10) },
            new Member { MemberID = 2, FullName = "Hoang Thuy Linh", PackageID = 2, JoinDate = new DateTime(2026, 2, 15) },
            new Member { MemberID = 3, FullName = "Do Minh Quan", PackageID = 3, JoinDate = new DateTime(2026, 3, 1) },
            new Member { MemberID = 4, FullName = "Phan Thanh Thao", PackageID = 4, JoinDate = new DateTime(2026, 3, 12) },
            new Member { MemberID = 5, FullName = "Bui Tien Dung", PackageID = 5, JoinDate = new DateTime(2026, 4, 5) }
        };

        public static List<Trainer> Trainers = new()
        {
            new Trainer { TrainerID = 1, TrainerName = "HLV Nguyen Manh", Email = "manh.nguyen@gym.com", ExperienceYears = 5 },
            new Trainer { TrainerID = 2, TrainerName = "HLV Tran Thao", Email = "thao.tran@gym.com", ExperienceYears = 3 },
            new Trainer { TrainerID = 3, TrainerName = "HLV Le Hai", Email = "hai.le@gym.com", ExperienceYears = 7 },
            new Trainer { TrainerID = 4, TrainerName = "HLV Pham Huy", Email = "huy.pham@gym.com", ExperienceYears = 2 },
            new Trainer { TrainerID = 5, TrainerName = "HLV Vu Hoang", Email = "hoang.vu@gym.com", ExperienceYears = 4 }
        };

        public static List<Specialization> Specializations = new()
        {
            new Specialization { SpecID = 1, SpecName = "Giam can nhanh", Description = "Bai tap cuong do cao tieu hao calo" },
            new Specialization { SpecID = 2, SpecName = "Tang co bap", Description = "Tap ta va dinh duong the hinh chuyen sau" },
            new Specialization { SpecID = 3, SpecName = "Yoga va Thien", Description = "Can bang tam tri va nang cao do deo dai" },
            new Specialization { SpecID = 4, SpecName = "Boxing va Vo thuat", Description = "Tap luyen phan xa va tu ve" },
            new Specialization { SpecID = 5, SpecName = "Phuc hoi chuc nang", Description = "Ho tro sau chan thuong xuong khop" }
        };

        public static List<Booking> Bookings = new()
        {
            new Booking { BookingID = 1, TrainerID = 1, BookingDate = new DateTime(2026, 6, 25, 8, 0, 0), SessionTime = new DateTime(2026, 6, 26, 9, 0, 0) },
            new Booking { BookingID = 2, TrainerID = 2, BookingDate = new DateTime(2026, 6, 25, 9, 30, 0), SessionTime = new DateTime(2026, 6, 26, 14, 0, 0) },
            new Booking { BookingID = 3, TrainerID = 3, BookingDate = new DateTime(2026, 6, 26, 10, 0, 0), SessionTime = new DateTime(2026, 6, 27, 16, 30, 0) },
            new Booking { BookingID = 4, TrainerID = 4, BookingDate = new DateTime(2026, 6, 26, 15, 0, 0), SessionTime = new DateTime(2026, 6, 27, 8, 0, 0) },
            new Booking { BookingID = 5, TrainerID = 5, BookingDate = new DateTime(2026, 6, 27, 11, 15, 0), SessionTime = new DateTime(2026, 6, 28, 19, 0, 0) }
        };

        public static List<BookingDetail> BookingDetails = new()
        {
            new BookingDetail { BookingID = 1, MemberID = 1, DurationMinutes = 60, Status = "Da tap" },
            new BookingDetail { BookingID = 2, MemberID = 2, DurationMinutes = 90, Status = "Da tap" },
            new BookingDetail { BookingID = 3, MemberID = 3, DurationMinutes = 60, Status = "Cho mien" },
            new BookingDetail { BookingID = 4, MemberID = 4, DurationMinutes = 45, Status = "Da huy" },
            new BookingDetail { BookingID = 5, MemberID = 5, DurationMinutes = 120, Status = "Da tap" }
        };

        public static List<TrainerSpec> TrainerSpecs = new()
        {
            new TrainerSpec { TrainerID = 1, SpecID = 1 },
            new TrainerSpec { TrainerID = 1, SpecID = 2 },
            new TrainerSpec { TrainerID = 2, SpecID = 3 },
            new TrainerSpec { TrainerID = 3, SpecID = 2 },
            new TrainerSpec { TrainerID = 3, SpecID = 4 },
            new TrainerSpec { TrainerID = 4, SpecID = 1 },
            new TrainerSpec { TrainerID = 5, SpecID = 5 }
        };
    }
}

namespace GivenAPIs.Controllers
{
    using GivenAPIs.Models;
    using GivenAPIs.Data;

    [ApiController]
    public class GymApiController : ControllerBase
    {
        // GET /api/booking-statuses - Trả về danh sách trạng thái đặt lịch
        [HttpGet("api/booking-statuses")]
        public IActionResult GetBookingStatuses()
        {
            var result = DataInitializer.BookingDetails
                .Select(bd => bd.Status)
                .Distinct()
                .ToList();
            return Ok(result);
        }

        // GET /api/members/search?memberName={name}&bookingStatus={status} - Bộ lọc nâng cao phối hợp Input và Dropdown
        [HttpGet("api/members/search")]
        public IActionResult SearchMembers([FromQuery] string? memberName = null, [FromQuery] string? bookingStatus = "All statuses")
        {
            var query = DataInitializer.Members.AsQueryable();

            // 1. Lọc tương đối theo tên hội viên (không phân biệt chữ hoa thường)
            if (!string.IsNullOrEmpty(memberName))
            {
                query = query.Where(m => m.FullName.Contains(memberName, StringComparison.OrdinalIgnoreCase));
            }

            // 2. Lọc chính xác theo Trạng thái đặt lịch của buổi tập (lọc thông qua bảng con BookingDetails)
            if (!string.IsNullOrEmpty(bookingStatus) && !bookingStatus.Equals("All statuses", StringComparison.OrdinalIgnoreCase))
            {
                var memberIdsWithStatus = DataInitializer.BookingDetails
                    .Where(bd => bd.Status.Equals(bookingStatus, StringComparison.OrdinalIgnoreCase))
                    .Select(bd => bd.MemberID);

                query = query.Where(m => memberIdsWithStatus.Contains(m.MemberID));
            }

            var result = query.AsEnumerable().Select(m => {
                var pkg = DataInitializer.MembershipPackages.First(p => p.PackageID == m.PackageID);
                return new MemberSearchDTO
                {
                    MemberID = m.MemberID,
                    FullName = m.FullName,
                    JoinDate = m.JoinDate,
                    PackageName = pkg.PackageName
                };
            }).ToList();

            return Ok(result);
        }

        // GET /api/members/{memberId} - Lấy chi tiết thông tin hội viên và lịch sử các buổi tập
        [HttpGet("api/members/{memberId}")]
        public IActionResult GetMemberDetails(int memberId)
        {
            var member = DataInitializer.Members.FirstOrDefault(m => m.MemberID == memberId);
            if (member == null) return NotFound();

            var pkg = DataInitializer.MembershipPackages.First(p => p.PackageID == member.PackageID);

            // Tìm kiếm lịch sử các buổi tập cá nhân của hội viên này
            var sessions = (from bd in DataInitializer.BookingDetails
                            join b in DataInitializer.Bookings on bd.BookingID equals b.BookingID
                            join t in DataInitializer.Trainers on b.TrainerID equals t.TrainerID
                            where bd.MemberID == memberId
                            select new MemberSessionDTO
                            {
                                BookingID = b.BookingID,
                                TrainerName = t.TrainerName,
                                SessionTime = b.SessionTime,
                                DurationMinutes = bd.DurationMinutes,
                                Status = bd.Status
                            }).ToList();

            var result = new MemberDetailDTO
            {
                MemberID = member.MemberID,
                FullName = member.FullName,
                JoinDate = member.JoinDate,
                PackageName = pkg.PackageName,
                PackagePrice = pkg.Price,
                Sessions = sessions
            };

            return Ok(result);
        }
    }
}