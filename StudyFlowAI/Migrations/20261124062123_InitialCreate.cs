using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyFlowAI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, defaultValue: ""),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonHoc",
                columns: table => new
                {
                    MaMonHoc = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaNguoiDung = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenMonHoc = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NgayThi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiemMucTieu = table.Column<double>(type: "float", nullable: true),
                    DoKho = table.Column<int>(type: "int", nullable: false),
                    MaMau = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "#3b82f6"),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonHoc", x => x.MaMonHoc);
                    table.ForeignKey(
                        name: "FK_MonHoc_AspNetUsers_MaNguoiDung",
                        column: x => x.MaNguoiDung,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaiLieu",
                columns: table => new
                {
                    MaTaiLieu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaMonHoc = table.Column<int>(type: "int", nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DuongDanFile = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NoiDungTrichXuat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TomTatAI = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NgayTaiLen = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiLieu", x => x.MaTaiLieu);
                    table.ForeignKey(
                        name: "FK_TaiLieu_MonHoc_MaMonHoc",
                        column: x => x.MaMonHoc,
                        principalTable: "MonHoc",
                        principalColumn: "MaMonHoc",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeThi",
                columns: table => new
                {
                    MaDeThi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiLieu = table.Column<int>(type: "int", nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DoKho = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "TrungBinh"),
                    ThoiGianLamPhut = table.Column<int>(type: "int", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeThi", x => x.MaDeThi);
                    table.ForeignKey(
                        name: "FK_DeThi_TaiLieu_MaTaiLieu",
                        column: x => x.MaTaiLieu,
                        principalTable: "TaiLieu",
                        principalColumn: "MaTaiLieu",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KeHoachHocTap",
                columns: table => new
                {
                    MaKeHoach = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaMonHoc = table.Column<int>(type: "int", nullable: false),
                    MaTaiLieu = table.Column<int>(type: "int", nullable: true),
                    TieuDe = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NgayHoc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GioBatDau = table.Column<TimeSpan>(type: "time", nullable: false),
                    ThoiLuongPhut = table.Column<int>(type: "int", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "ChuaLam"),
                    DoAITao = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeHoachHocTap", x => x.MaKeHoach);
                    table.ForeignKey(
                        name: "FK_KeHoachHocTap_MonHoc_MaMonHoc",
                        column: x => x.MaMonHoc,
                        principalTable: "MonHoc",
                        principalColumn: "MaMonHoc",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KeHoachHocTap_TaiLieu_MaTaiLieu",
                        column: x => x.MaTaiLieu,
                        principalTable: "TaiLieu",
                        principalColumn: "MaTaiLieu",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CauHoi",
                columns: table => new
                {
                    MaCauHoi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaDeThi = table.Column<int>(type: "int", nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DapAnA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DapAnB = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DapAnC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DapAnD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DapAnDung = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    GiaiThichAI = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CauHoi", x => x.MaCauHoi);
                    table.ForeignKey(
                        name: "FK_CauHoi_DeThi_MaDeThi",
                        column: x => x.MaDeThi,
                        principalTable: "DeThi",
                        principalColumn: "MaDeThi",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LuotLamBai",
                columns: table => new
                {
                    MaLuotLam = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaDeThi = table.Column<int>(type: "int", nullable: false),
                    MaNguoiDung = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DiemSo = table.Column<double>(type: "float", nullable: false),
                    SoCauDung = table.Column<int>(type: "int", nullable: false),
                    TongSoCau = table.Column<int>(type: "int", nullable: false),
                    ThoiGianBatDau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThoiGianNopBai = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NhanXetAI = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuotLamBai", x => x.MaLuotLam);
                    table.ForeignKey(
                        name: "FK_LuotLamBai_AspNetUsers_MaNguoiDung",
                        column: x => x.MaNguoiDung,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LuotLamBai_DeThi_MaDeThi",
                        column: x => x.MaDeThi,
                        principalTable: "DeThi",
                        principalColumn: "MaDeThi",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhienPomodoro",
                columns: table => new
                {
                    MaPhien = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaNguoiDung = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MaKeHoach = table.Column<int>(type: "int", nullable: true),
                    ThoiLuongPhut = table.Column<int>(type: "int", nullable: false),
                    ThoiDiemHoanThanh = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhienPomodoro", x => x.MaPhien);
                    table.ForeignKey(
                        name: "FK_PhienPomodoro_AspNetUsers_MaNguoiDung",
                        column: x => x.MaNguoiDung,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhienPomodoro_KeHoachHocTap_MaKeHoach",
                        column: x => x.MaKeHoach,
                        principalTable: "KeHoachHocTap",
                        principalColumn: "MaKeHoach",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietTraLoi",
                columns: table => new
                {
                    MaChiTiet = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaLuotLam = table.Column<int>(type: "int", nullable: false),
                    MaCauHoi = table.Column<int>(type: "int", nullable: false),
                    DapAnChon = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    LaDapAnDung = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietTraLoi", x => x.MaChiTiet);
                    table.ForeignKey(
                        name: "FK_ChiTietTraLoi_CauHoi_MaCauHoi",
                        column: x => x.MaCauHoi,
                        principalTable: "CauHoi",
                        principalColumn: "MaCauHoi",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietTraLoi_LuotLamBai_MaLuotLam",
                        column: x => x.MaLuotLam,
                        principalTable: "LuotLamBai",
                        principalColumn: "MaLuotLam",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CauHoi_MaDeThi",
                table: "CauHoi",
                column: "MaDeThi");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietTraLoi_MaCauHoi",
                table: "ChiTietTraLoi",
                column: "MaCauHoi");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietTraLoi_MaLuotLam",
                table: "ChiTietTraLoi",
                column: "MaLuotLam");

            migrationBuilder.CreateIndex(
                name: "IX_DeThi_MaTaiLieu",
                table: "DeThi",
                column: "MaTaiLieu");

            migrationBuilder.CreateIndex(
                name: "IX_KeHoachHocTap_MaMonHoc",
                table: "KeHoachHocTap",
                column: "MaMonHoc");

            migrationBuilder.CreateIndex(
                name: "IX_KeHoachHocTap_MaTaiLieu",
                table: "KeHoachHocTap",
                column: "MaTaiLieu");

            migrationBuilder.CreateIndex(
                name: "IX_LuotLamBai_MaDeThi",
                table: "LuotLamBai",
                column: "MaDeThi");

            migrationBuilder.CreateIndex(
                name: "IX_LuotLamBai_MaNguoiDung",
                table: "LuotLamBai",
                column: "MaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_MonHoc_MaNguoiDung",
                table: "MonHoc",
                column: "MaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_PhienPomodoro_MaKeHoach",
                table: "PhienPomodoro",
                column: "MaKeHoach");

            migrationBuilder.CreateIndex(
                name: "IX_PhienPomodoro_MaNguoiDung",
                table: "PhienPomodoro",
                column: "MaNguoiDung");

            migrationBuilder.CreateIndex(
                name: "IX_TaiLieu_MaMonHoc",
                table: "TaiLieu",
                column: "MaMonHoc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ChiTietTraLoi");

            migrationBuilder.DropTable(
                name: "PhienPomodoro");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "CauHoi");

            migrationBuilder.DropTable(
                name: "LuotLamBai");

            migrationBuilder.DropTable(
                name: "KeHoachHocTap");

            migrationBuilder.DropTable(
                name: "DeThi");

            migrationBuilder.DropTable(
                name: "TaiLieu");

            migrationBuilder.DropTable(
                name: "MonHoc");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
