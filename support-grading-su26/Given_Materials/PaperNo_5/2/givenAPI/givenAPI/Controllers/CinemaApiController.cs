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

    // Thực thể liên kết mô phỏng trong bộ nhớ để gán Combo cho hóa đơn mua vé
    public class TicketCombo
    {
        public int TicketID { get; set; }
        public int ComboID { get; set; }
        public int Quantity { get; set; }
    }

    // --- Data Transfer Objects (DTOs) trả về cho Client ---
    public class GenreDTO
    {
        public int GenreID { get; set; }
        public string GenreName { get; set; } = null!;
    }

    public class MovieSearchDTO
    {
        public int MovieID { get; set; }
        public string Title { get; set; } = null!;
        public int Duration { get; set; }
        public decimal BasePrice { get; set; }
        public List<string> Genres { get; set; } = new();
    }

    public class ComboAnalysisDTO
    {
        public int ComboID { get; set; }
        public string ComboName { get; set; } = null!;
        public decimal ComboPrice { get; set; }
    }

    public class MovieDetailDTO
    {
        public int MovieID { get; set; }
        public string Title { get; set; } = null!;
        public decimal BasePrice { get; set; }
        public List<string> Genres { get; set; } = new();
        public List<ComboAnalysisDTO> PurchasedCombos { get; set; } = new();
    }
}

namespace GivenAPIs.Data
{
    using GivenAPIs.Models;

    public static class InitialData
    {
        public static List<Genre> Genres = new()
        {
            new Genre { GenreID = 1, GenreName = "Hanh dong", Description = "Phim kich tinh, hanh dong gay can" },
            new Genre { GenreID = 2, GenreName = "Kinh di", Description = "Phim kinh di tam linh quai di" },
            new Genre { GenreID = 3, GenreName = "Hoat hinh", Description = "Phim do hoa hoat hoa thieu nhi" },
            new Genre { GenreID = 4, GenreName = "Hai huoc", Description = "Phim cuoi xua tan cang thang" },
            new Genre { GenreID = 5, GenreName = "Tinh cam", Description = "Phim tinh yeu doi lua sau sac" }
        };

        public static List<Movie> Movies = new()
        {
            new Movie { MovieID = 1, Title = "Lat Mat 7", Duration = 120, BasePrice = 90000.00m }, // Premium
            new Movie { MovieID = 2, Title = "Ngay Xua Co Mot Chuyen Tinh", Duration = 110, BasePrice = 85000.00m }, // Budget
            new Movie { MovieID = 3, Title = "Biet Doi Anh Hung", Duration = 150, BasePrice = 120000.00m }, // Premium
            new Movie { MovieID = 4, Title = "Tham Tu Conan", Duration = 105, BasePrice = 80000.00m }, // Budget
            new Movie { MovieID = 5, Title = "Quy Cau", Duration = 95, BasePrice = 95000.00m } // Premium
        };

        public static List<Viewer> Viewers = new()
        {
            new Viewer { ViewerID = 1, FullName = "Tran Van Hung", Email = "hung.tran@gmail.com", Age = 20 },
            new Viewer { ViewerID = 2, FullName = "Nguyen Thi Mai", Email = "mai.nguyen@gmail.com", Age = 17 },
            new Viewer { ViewerID = 3, FullName = "Le Hoang Long", Email = "long.le@gmail.com", Age = 25 },
            new Viewer { ViewerID = 4, FullName = "Pham Hong Nhung", Email = "nhung.pham@gmail.com", Age = 15 },
            new Viewer { ViewerID = 5, FullName = "Vu Minh Tri", Email = "tri.vu@gmail.com", Age = 30 }
        };

        public static List<Combo> Combos = new()
        {
            new Combo { ComboID = 1, ComboName = "Bap Rang Bo Single", Price = 45000.00m }, // Premium (> 40K taxed)
            new Combo { ComboID = 2, ComboName = "Nuoc Ngot Lon", Price = 30000.00m },       // Standard
            new Combo { ComboID = 3, ComboName = "Combo Doi Bap Nuoc", Price = 95000.00m },  // Premium
            new Combo { ComboID = 4, ComboName = "Kho Ga Xe Cay", Price = 35000.00m },       // Standard (35K * 1.05 = 36.75K)
            new Combo { ComboID = 5, ComboName = "Xuc Xich Nuong", Price = 25000.00m }       // Standard
        };

        public static List<MovieGenre> MovieGenres = new()
        {
            new MovieGenre { MovieID = 1, GenreID = 1 }, new MovieGenre { MovieID = 1, GenreID = 4 },
            new MovieGenre { MovieID = 2, GenreID = 5 },
            new MovieGenre { MovieID = 3, GenreID = 1 },
            new MovieGenre { MovieID = 4, GenreID = 3 }, new MovieGenre { MovieID = 4, GenreID = 4 },
            new MovieGenre { MovieID = 5, GenreID = 2 }
        };

        public static List<Ticket> Tickets = new()
        {
            new Ticket { TicketID = 1, ViewerID = 1, BookingDate = DateTime.Now, ShowTime = DateTime.Now.AddDays(1) },
            new Ticket { TicketID = 2, ViewerID = 2, BookingDate = DateTime.Now, ShowTime = DateTime.Now.AddDays(1) },
            new Ticket { TicketID = 3, ViewerID = 3, BookingDate = DateTime.Now, ShowTime = DateTime.Now.AddDays(2) },
            new Ticket { TicketID = 4, ViewerID = 4, BookingDate = DateTime.Now, ShowTime = DateTime.Now.AddDays(2) },
            new Ticket { TicketID = 5, ViewerID = 5, BookingDate = DateTime.Now, ShowTime = DateTime.Now.AddDays(2) }
        };

        public static List<TicketDetail> TicketDetails = new()
        {
            new TicketDetail { TicketID = 1, MovieID = 1, SeatNumber = "H12", Quantity = 2 },
            new TicketDetail { TicketID = 2, MovieID = 2, SeatNumber = "A05", Quantity = 1 },
            new TicketDetail { TicketID = 3, MovieID = 3, SeatNumber = "F09", Quantity = 3 },
            new TicketDetail { TicketID = 4, MovieID = 4, SeatNumber = "B01", Quantity = 1 },
            new TicketDetail { TicketID = 5, MovieID = 5, SeatNumber = "G11", Quantity = 2 }
        };

        
        public static List<TicketCombo> TicketCombos = new()
        {
            new TicketCombo { TicketID = 1, ComboID = 1, Quantity = 2 }, // Khách Hung mua Bap Single khi xem Lat Mat 7
            new TicketCombo { TicketID = 1, ComboID = 2, Quantity = 2 }, // Khách Hung mua Nuoc Ngot khi xem Lat Mat 7
            new TicketCombo { TicketID = 2, ComboID = 4, Quantity = 1 }, // Khách Mai mua Kho Ga khi xem Ngay Xua...
            new TicketCombo { TicketID = 3, ComboID = 3, Quantity = 1 }, // Khách Long mua Combo Doi khi xem Biet Doi...
            new TicketCombo { TicketID = 5, ComboID = 1, Quantity = 1 }, // Khách Tri mua Bap Single khi xem Quy Cau
            new TicketCombo { TicketID = 5, ComboID = 5, Quantity = 2 }  // Khách Tri mua Xuc Xich khi xem Quy Cau
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
        // GET /api/genres
        [HttpGet("api/genres")]
        public IActionResult GetGenres()
        {
            var result = InitialData.Genres.Select(g => new GenreDTO
            {
                GenreID = g.GenreID,
                GenreName = g.GenreName
            }).ToList();
            return Ok(result);
        }

        // GET /api/movies/search?genreId=1&priceSegment=Premium
        [HttpGet("api/movies/search")]
        public IActionResult SearchMovies(int genreId = 0, string priceSegment = "All prices")
        {
            var query = InitialData.Movies.AsQueryable();

            // Lọc theo thể loại phim (n-n)
            if (genreId > 0)
            {
                var movieIds = InitialData.MovieGenres
                    .Where(mg => mg.GenreID == genreId)
                    .Select(mg => mg.MovieID);
                query = query.Where(m => movieIds.Contains(m.MovieID));
            }

            // Lọc theo phân khúc giá vé
            if (priceSegment == "Budget ( < 90K )")
            {
                query = query.Where(m => m.BasePrice < 90000.00m);
            }
            else if (priceSegment == "Premium ( >= 90K )")
            {
                query = query.Where(m => m.BasePrice >= 90000.00m);
            }

            var result = query.Select(m => new MovieSearchDTO
            {
                MovieID = m.MovieID,
                Title = m.Title,
                Duration = m.Duration,
                BasePrice = m.BasePrice,
                Genres = (from mg in InitialData.MovieGenres
                          join g in InitialData.Genres on mg.GenreID equals g.GenreID
                          where mg.MovieID == m.MovieID
                          select g.GenreName).ToList()
            }).ToList();

            return Ok(result);
        }

        // GET /api/movies/{id}
        [HttpGet("api/movies/{id}")]
        public IActionResult GetMovieDetail(int id)
        {
            var movie = InitialData.Movies.FirstOrDefault(m => m.MovieID == id);
            if (movie == null) return NotFound();

            // Lấy danh sách thể loại của phim
            var genres = (from mg in InitialData.MovieGenres
                          join g in InitialData.Genres on mg.GenreID equals g.GenreID
                          where mg.MovieID == id
                          select g.GenreName).ToList();

            // Tìm các vé đã đặt xem bộ phim này
            var ticketIdsOfMovie = InitialData.TicketDetails
                .Where(td => td.MovieID == id)
                .Select(td => td.TicketID)
                .ToList();

            // Phân tích các Combo đồ ăn đi kèm các vé xem phim này
            var combos = (from tc in InitialData.TicketCombos
                          join c in InitialData.Combos on tc.ComboID equals c.ComboID
                          where ticketIdsOfMovie.Contains(tc.TicketID)
                          select new ComboAnalysisDTO
                          {
                              ComboID = c.ComboID,
                              ComboName = c.ComboName,
                              ComboPrice = c.Price
                          }).DistinctBy(c => c.ComboID).ToList();

            var result = new MovieDetailDTO
            {
                MovieID = movie.MovieID,
                Title = movie.Title,
                BasePrice = movie.BasePrice,
                Genres = genres,
                PurchasedCombos = combos
            };

            return Ok(result);
        }
    }
}