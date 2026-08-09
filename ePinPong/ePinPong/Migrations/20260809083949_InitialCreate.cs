using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePinPong.Migrations
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
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Ime = table.Column<string>(type: "TEXT", nullable: false),
                    Prezime = table.Column<string>(type: "TEXT", nullable: false),
                    Grad = table.Column<string>(type: "TEXT", nullable: false),
                    DatumRodjenja = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatumRegistracije = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
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
                    LoginProvider = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
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
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
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
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "Lige",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naziv = table.Column<string>(type: "TEXT", nullable: false),
                    Opis = table.Column<string>(type: "TEXT", nullable: false),
                    Sezona = table.Column<string>(type: "TEXT", nullable: false),
                    DatumPocetka = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BrojRegularnihTurnira = table.Column<int>(type: "INTEGER", nullable: false),
                    OrganizatorId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lige", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Lige_AspNetUsers_OrganizatorId",
                        column: x => x.OrganizatorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifikacije",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sadrzaj = table.Column<string>(type: "TEXT", nullable: false),
                    DatumKreiranja = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Procitana = table.Column<bool>(type: "INTEGER", nullable: false),
                    KorisnikId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifikacije", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Notifikacije_AspNetUsers_KorisnikId",
                        column: x => x.KorisnikId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pracenja",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PratilacID = table.Column<string>(type: "TEXT", nullable: false),
                    PraceniID = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pracenja", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Pracenja_AspNetUsers_PraceniID",
                        column: x => x.PraceniID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pracenja_AspNetUsers_PratilacID",
                        column: x => x.PratilacID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Turniri",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naziv = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DatumPocetka = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatumKraja = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MaxIgraca = table.Column<int>(type: "INTEGER", nullable: false),
                    Lokacija = table.Column<string>(type: "TEXT", nullable: false),
                    Opis = table.Column<string>(type: "TEXT", nullable: false),
                    SlikaUrl = table.Column<string>(type: "TEXT", nullable: true),
                    TipTakmicenja = table.Column<int>(type: "INTEGER", nullable: false),
                    SistemTurnira = table.Column<int>(type: "INTEGER", nullable: false),
                    OrganizatorId = table.Column<string>(type: "TEXT", nullable: false),
                    LigaID = table.Column<int>(type: "INTEGER", nullable: true),
                    Kolo = table.Column<int>(type: "INTEGER", nullable: true),
                    PobjednikID = table.Column<string>(type: "TEXT", nullable: true),
                    DrugoplasiraniID = table.Column<string>(type: "TEXT", nullable: true),
                    TrecaplasiraniID = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turniri", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Turniri_AspNetUsers_OrganizatorId",
                        column: x => x.OrganizatorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turniri_Lige_LigaID",
                        column: x => x.LigaID,
                        principalTable: "Lige",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mecevi",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TurnirID = table.Column<int>(type: "INTEGER", nullable: false),
                    Igrac1ID = table.Column<string>(type: "TEXT", nullable: true),
                    Igrac2ID = table.Column<string>(type: "TEXT", nullable: true),
                    Igrac1PartnerID = table.Column<string>(type: "TEXT", nullable: true),
                    Igrac2PartnerID = table.Column<string>(type: "TEXT", nullable: true),
                    PoeniIgrac1 = table.Column<int>(type: "INTEGER", nullable: true),
                    PoeniIgrac2 = table.Column<int>(type: "INTEGER", nullable: true),
                    VrijemeMeca = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Runda = table.Column<int>(type: "INTEGER", nullable: false),
                    Odigran = table.Column<bool>(type: "INTEGER", nullable: false),
                    TipMeca = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchCode = table.Column<string>(type: "TEXT", nullable: false),
                    WinnerNextMatchCode = table.Column<string>(type: "TEXT", nullable: true),
                    LoserNextMatchCode = table.Column<string>(type: "TEXT", nullable: true),
                    WinnerNextMatchSlot = table.Column<int>(type: "INTEGER", nullable: true),
                    LoserNextMatchSlot = table.Column<int>(type: "INTEGER", nullable: true),
                    PlacingRange = table.Column<string>(type: "TEXT", nullable: false),
                    NazivGrupe = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mecevi", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Mecevi_AspNetUsers_Igrac1ID",
                        column: x => x.Igrac1ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mecevi_AspNetUsers_Igrac1PartnerID",
                        column: x => x.Igrac1PartnerID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mecevi_AspNetUsers_Igrac2ID",
                        column: x => x.Igrac2ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mecevi_AspNetUsers_Igrac2PartnerID",
                        column: x => x.Igrac2PartnerID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mecevi_Turniri_TurnirID",
                        column: x => x.TurnirID,
                        principalTable: "Turniri",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Registracije",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TurnirID = table.Column<int>(type: "INTEGER", nullable: false),
                    KorisnikID = table.Column<string>(type: "TEXT", nullable: false),
                    DatumRegistracije = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Odobren = table.Column<bool>(type: "INTEGER", nullable: false),
                    Sesir = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registracije", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Registracije_AspNetUsers_KorisnikID",
                        column: x => x.KorisnikID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Registracije_Turniri_TurnirID",
                        column: x => x.TurnirID,
                        principalTable: "Turniri",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TurnirParovi",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TurnirID = table.Column<int>(type: "INTEGER", nullable: false),
                    Igrac1ID = table.Column<string>(type: "TEXT", nullable: false),
                    Igrac2ID = table.Column<string>(type: "TEXT", nullable: false),
                    DatumPrijave = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnirParovi", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TurnirParovi_AspNetUsers_Igrac1ID",
                        column: x => x.Igrac1ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurnirParovi_AspNetUsers_Igrac2ID",
                        column: x => x.Igrac2ID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurnirParovi_Turniri_TurnirID",
                        column: x => x.TurnirID,
                        principalTable: "Turniri",
                        principalColumn: "ID",
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
                unique: true);

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
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lige_OrganizatorId",
                table: "Lige",
                column: "OrganizatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Mecevi_Igrac1ID",
                table: "Mecevi",
                column: "Igrac1ID");

            migrationBuilder.CreateIndex(
                name: "IX_Mecevi_Igrac1PartnerID",
                table: "Mecevi",
                column: "Igrac1PartnerID");

            migrationBuilder.CreateIndex(
                name: "IX_Mecevi_Igrac2ID",
                table: "Mecevi",
                column: "Igrac2ID");

            migrationBuilder.CreateIndex(
                name: "IX_Mecevi_Igrac2PartnerID",
                table: "Mecevi",
                column: "Igrac2PartnerID");

            migrationBuilder.CreateIndex(
                name: "IX_Mecevi_TurnirID",
                table: "Mecevi",
                column: "TurnirID");

            migrationBuilder.CreateIndex(
                name: "IX_Notifikacije_KorisnikId",
                table: "Notifikacije",
                column: "KorisnikId");

            migrationBuilder.CreateIndex(
                name: "IX_Pracenja_PraceniID",
                table: "Pracenja",
                column: "PraceniID");

            migrationBuilder.CreateIndex(
                name: "IX_Pracenja_PratilacID",
                table: "Pracenja",
                column: "PratilacID");

            migrationBuilder.CreateIndex(
                name: "IX_Registracije_KorisnikID",
                table: "Registracije",
                column: "KorisnikID");

            migrationBuilder.CreateIndex(
                name: "IX_Registracije_TurnirID",
                table: "Registracije",
                column: "TurnirID");

            migrationBuilder.CreateIndex(
                name: "IX_Turniri_LigaID",
                table: "Turniri",
                column: "LigaID");

            migrationBuilder.CreateIndex(
                name: "IX_Turniri_OrganizatorId",
                table: "Turniri",
                column: "OrganizatorId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnirParovi_Igrac1ID",
                table: "TurnirParovi",
                column: "Igrac1ID");

            migrationBuilder.CreateIndex(
                name: "IX_TurnirParovi_Igrac2ID",
                table: "TurnirParovi",
                column: "Igrac2ID");

            migrationBuilder.CreateIndex(
                name: "IX_TurnirParovi_TurnirID",
                table: "TurnirParovi",
                column: "TurnirID");
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
                name: "Mecevi");

            migrationBuilder.DropTable(
                name: "Notifikacije");

            migrationBuilder.DropTable(
                name: "Pracenja");

            migrationBuilder.DropTable(
                name: "Registracije");

            migrationBuilder.DropTable(
                name: "TurnirParovi");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Turniri");

            migrationBuilder.DropTable(
                name: "Lige");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
