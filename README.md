# PRN232 Auto Grader - Server (Backend)

Hệ thống chấm thi tự động cho môn PRN232 (lập trình ASP.NET). Hỗ trợ 2 dạng câu hỏi: **Q1** (SQL/Stored Procedures) và **Q2** (ASP.NET Razor API). Chấm bài theo luồng bất đồng bộ qua RabbitMQ.

---

## Mục lục

- [Tổng quan kiến trúc](#tổng-quan-kiến-trúc)
- [Tech Stack](#tech-stack)
- [Cài đặt & Chạy](#cài-đặt--chạy)
- [API Endpoints](#api-endpoints)
- [Biến môi trường](#biến-môi-trường)

---

## Tổng quan kiến trúc

Backend được xây dựng dựa trên Clean Architecture, bao gồm:

- **GradingSystem.Api**: REST API xử lý request từ Frontend.
- **GradingSystem.Worker**: Background Worker xử lý việc chấm thi bất đồng bộ qua RabbitMQ.
- **GradingSystem.Application**: Chứa business logic, DTOs, interfaces.
- **GradingSystem.Domain**: Chứa các Entity cốt lõi.
- **GradingSystem.Infrastructure**: Kết nối DB (PostgreSQL, SQL Server), Message Broker.

## Tech Stack

| Thành phần         | Công nghệ                                    |
| ------------------ | -------------------------------------------- |
| Backend API        | ASP.NET Core 8, EF Core 8, API Versioning    |
| Worker             | .NET 8 Background Service, MassTransit 8.3.6 |
| Message Broker     | RabbitMQ 3                                   |
| Database chính     | PostgreSQL 16                                |
| Database sinh viên | SQL Server 2022 (Q1)                         |

## Cài đặt & Chạy

Yêu cầu: Docker Desktop, .NET 8 SDK.

1. **Copy cấu hình môi trường:**

   ```bash
   cp .env.example .env
   ```

2. **Khởi động Infrastructure (DB, RabbitMQ):**

   ```bash
   docker compose -f docker-compose.dev.yml up -d
   ```

3. **Chạy Backend API:**

   ```bash
   cd GradingSystem.Api
   dotnet run
   ```

4. **Chạy Worker (Chấm bài):**
   ```bash
   cd GradingSystem.Worker
   dotnet run
   ```

## Biến môi trường

Xem `.env.example` để biết cấu hình chi tiết cho PostgreSQL, SQL Server, và RabbitMQ.

<!--
1. Test structure
2. Test case Q2
3. Cleanup worker
4. Lần chấm bài
-->
