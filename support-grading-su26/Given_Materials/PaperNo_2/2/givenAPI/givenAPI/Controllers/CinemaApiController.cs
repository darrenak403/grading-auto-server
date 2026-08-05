using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GivenAPIs.Models
{
    // --- Các thực thể Entity mô phỏng theo cấu trúc Database ---
    public class Genre
    {
        public int GenreID { get; set; }
        public string GenreName { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class Movie
    {
        public int MovieID { get; set; }
        public string Title { get; set; } = null!;
        public int Duration { get; set; }
        public decimal BasePrice { get; set; }
    }

    public class Viewer
    {
        public int ViewerID { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int Age { get; set; }
    }

    public class Combo
    {
        public int ComboID { get; set; }
        public string ComboName { get; set; } = null!;
        public decimal Price { get; set; }
    }

    public class Ticket
    {
        public int TicketID { get; set; }
        public int ViewerID { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime ShowTime { get; set; }
    }

    public class TicketDetail
    {
        public int TicketID { get; set; }
        public int MovieID { get; set; }
        public string SeatNumber { get; set; } = null!;
        public int Quantity { get; set; }
    }

    public class MovieGenre
    {
        public int MovieID { get; set; }
        public int GenreID { get; set; }
    }

    // --- Data Transfer Objects (DTOs) dùng để trả về API ---
    public class MovieDTO
    {
        public int MovieID { get; set; }
        public string Title { get; set; } = null!;
    }

    public class TicketSearchDTO
    {
        public int TicketID { get; set; }
        public string ViewerName { get; set; } = null!;
        public DateTime BookingDate { get; set; }
        public int ViewerAge { get; set; }
    }

    public class MovieBookingDetailDTO
    {
        public int MovieID { get; set; }
        public string Title { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal BasePrice { get; set; }
    }

    public class TicketDetailDTO
    {
        public int TicketID { get; set; }
        public string ViewerEmail { get; set; } = null!;
        public DateTime ShowTime { get; set; }
        public List<MovieBookingDetailDTO> Details { get; set; } = new();
    }
}

namespace GivenAPIs.Data
{
    using GivenAPIs.Models;

    // --- Khởi tạo dữ liệu mẫu In-Memory để hỗ trợ test các kịch bản ---
    public static class InitialData
    {
        public static List<Genre> Genres = new()
        {
            new Genre { GenreID = 1, GenreName = "Hanh dong", Description = "Phim co nhieu canh duoi bat gay can" },
            new Genre { GenreID = 2, GenreName = "Kinh di", Description = "Phim co yeu to ma mi giat gan" },
            new Genre { GenreID = 3, GenreName = "Hoat hinh", Description = "Phim cho gia dinh va tre em" },
            new Genre { GenreID = 4, GenreName = "Hai huoc", Description = "Phim mang lai tieng cuoi" },
            new Genre { GenreID = 5, GenreName = "Tinh cam", Description = "Phim tam ly tinh cam lang man" }
        };

        public static List<Movie> Movies = new()
        {
            new Movie { MovieID = 1, Title = "Lat Mat 7", Duration = 120, BasePrice = 90000.00m },
            new Movie { MovieID = 2, Title = "Ngay Xua Co Mot Chuyen Tinh", Duration = 110, BasePrice = 85000.00m },
            new Movie { MovieID = 3, Title = "Biet Doi Anh Hung", Duration = 150, BasePrice = 120000.00m },
            new Movie { MovieID = 4, Title = "Tham Tu Conan", Duration = 105, BasePrice = 80000.00m },
            new Movie { MovieID = 5, Title = "Quy Cau", Duration = 95, BasePrice = 95000.00m }
        };

        public static List<Viewer> Viewers = new()
        {
            new Viewer { ViewerID = 1, FullName = "Tran Van Hung", Email = "hung.tran@gmail.com", Age = 20 },
            new Viewer { ViewerID = 2, FullName = "Nguyen Thi Mai", Email = "mai.nguyen@gmail.com", Age = 17 }, // Minor
            new Viewer { ViewerID = 3, FullName = "Le Hoang Long", Email = "long.le@gmail.com", Age = 25 },
            new Viewer { ViewerID = 4, FullName = "Pham Hong Nhung", Email = "nhung.pham@gmail.com", Age = 15 }, // Minor
            new Viewer { ViewerID = 5, FullName = "Vu Minh Tri", Email = "tri.vu@gmail.com", Age = 30 }
        };

        public static List<Ticket> Tickets = new()
        {
            new Ticket { TicketID = 1, ViewerID = 1, BookingDate = new DateTime(2026, 6, 20, 14, 0, 0), ShowTime = new DateTime(2026, 6, 21, 19, 30, 0) },
            new Ticket { TicketID = 2, ViewerID = 2, BookingDate = new DateTime(2026, 6, 20, 15, 30, 0), ShowTime = new DateTime(2026, 6, 21, 21, 0, 0) },
            new Ticket { TicketID = 3, ViewerID = 3, BookingDate = new DateTime(2026, 6, 21, 9, 0, 0), ShowTime = new DateTime(2026, 6, 22, 18, 0, 0) },
            new Ticket { TicketID = 4, ViewerID = 4, BookingDate = new DateTime(2026, 6, 21, 10, 15, 0), ShowTime = new DateTime(2026, 6, 22, 14, 0, 0) },
            new Ticket { TicketID = 5, ViewerID = 5, BookingDate = new DateTime(2026, 6, 22, 8, 0, 0), ShowTime = new DateTime(2026, 6, 22, 20, 30, 0) }
        };

        public static List<TicketDetail> TicketDetails = new()
        {
            new TicketDetail { TicketID = 1, MovieID = 1, SeatNumber = "H12", Quantity = 2 }, // Doanh thu: 2 * 90k = 180k (> 150k VIP)
            new TicketDetail { TicketID = 2, MovieID = 2, SeatNumber = "A05", Quantity = 1 }, // Doanh thu: 1 * 85k = 85k (Std)
            new TicketDetail { TicketID = 3, MovieID = 3, SeatNumber = "F09", Quantity = 3 }, // Doanh thu: 3 * 120k = 360k (> 150k VIP)
            new TicketDetail { TicketID = 4, MovieID = 4, SeatNumber = "B01", Quantity = 1 }, // Doanh thu: 1 * 80k = 80k (Std)
            new TicketDetail { TicketID = 5, MovieID = 5, SeatNumber = "G11", Quantity = 2 }  // Doanh thu: 2 * 95k = 190k (> 150k VIP)
        };

        public static List<MovieGenre> MovieGenres = new()
        {
            new MovieGenre { MovieID = 1, GenreID = 1 },
            new MovieGenre { MovieID = 1, GenreID = 4 },
            new MovieGenre { MovieID = 2, GenreID = 5 },
            new MovieGenre { MovieID = 3, GenreID = 1 },
            new MovieGenre { MovieID = 4, GenreID = 3 },
            new MovieGenre { MovieID = 4, GenreID = 4 },
            new MovieGenre { MovieID = 5, GenreID = 2 }
        };
    }
}

namespace GivenAPIs.Controllers
{
    using GivenAPIs.Models;
    using GivenAPIs.Data;

    [ApiController]
    public class CinemaApiController : ControllerBase
    {
        // GET /api/movies - Trả về danh sách phim cho Dropdown
        [HttpGet("api/movies")]
        public IActionResult GetMovies()
        {
            var result = InitialData.Movies.Select(m => new MovieDTO
            {
                MovieID = m.MovieID,
                Title = m.Title
            }).ToList();

            return Ok(result);
        }

        // GET /api/tickets/search?movieId={id}&ageGroup={group} - Tìm kiếm nâng cao
        [HttpGet("api/tickets/search")]
        public IActionResult SearchTickets(int movieId = 0, string ageGroup = "All viewers")
        {
            var query = InitialData.Tickets.AsQueryable();

            // Lọc theo Phim được đặt (nếu chọn cụ thể)
            if (movieId > 0)
            {
                var ticketIds = InitialData.TicketDetails
                    .Where(td => td.MovieID == movieId)
                    .Select(td => td.TicketID);

                query = query.Where(t => ticketIds.Contains(t.TicketID));
            }

            // Lọc theo nhóm độ tuổi của khán giả mua vé
            if (ageGroup == "Minor ( < 18 )")
            {
                var viewerIds = InitialData.Viewers.Where(v => v.Age < 18).Select(v => v.ViewerID);
                query = query.Where(t => viewerIds.Contains(t.ViewerID));
            }
            else if (ageGroup == "Adult ( >= 18 )")
            {
                var viewerIds = InitialData.Viewers.Where(v => v.Age >= 18).Select(v => v.ViewerID);
                query = query.Where(t => viewerIds.Contains(t.ViewerID));
            }

            var result = query
    .Join(InitialData.Viewers, t => t.ViewerID, v => v.ViewerID, (t, v) => new TicketSearchDTO
    {
        TicketID = t.TicketID,
        ViewerName = v.FullName,
        BookingDate = t.BookingDate,
        ViewerAge = v.Age
    })
    .ToList();

            return Ok(result);
        }

        // GET /api/tickets/{ticketId} - Chi tiết vé đặt
        [HttpGet("api/tickets/{ticketId}")]
        public IActionResult GetTicketDetails(int ticketId)
        {
            var ticket = InitialData.Tickets.FirstOrDefault(t => t.TicketID == ticketId);
            if (ticket == null) return NotFound();

            var viewer = InitialData.Viewers.First(v => v.ViewerID == ticket.ViewerID);

            var details = (from td in InitialData.TicketDetails
                           join m in InitialData.Movies on td.MovieID equals m.MovieID
                           where td.TicketID == ticketId
                           select new MovieBookingDetailDTO
                           {
                               MovieID = m.MovieID,
                               Title = m.Title,
                               Quantity = td.Quantity,
                               BasePrice = m.BasePrice
                           }).ToList();

            var result = new TicketDetailDTO
            {
                TicketID = ticket.TicketID,
                ViewerEmail = viewer.Email,
                ShowTime = ticket.ShowTime,
                Details = details
            };

            return Ok(result);
        }
    }
}