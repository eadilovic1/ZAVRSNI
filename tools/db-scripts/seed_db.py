import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import sqlite3, uuid, random, shutil
from datetime import datetime

DB_PATH = "epinpong.db"
BACKUP_PATH = "epinpong_seed_backup.db"
SEEDED_29_PATH = "epinpong_seeded_29.db"

random.seed(12345)

NAMES = [
    "Adnan","Amar","Armin","Adin","Aldin","Almir","Damir","Denis","Dino","Edin",
    "Eldin","Elmir","Emir","Enes","Ermin","Faruk","Haris","Jasmin","Kenan","Luka",
    "Mahir","Mario","Marko","Mirza","Muamer","Muris","Ned","Nermin","Niko","Omar",
    "Petar","Rasim","Samir","Sanel","Senad","Tarik","Tomislav","Vedran","Zlatan","Zoran",
    "Bojan","Davor","Filip","Igor","Ivan","Josip","Kristijan","Leon","Mateo","Nikola"
]
SURNAMES = [
    "Ahmetovic","Bajic","Becic","Begic","Colic","Covic","Delic","Djukic","Dzanic","Dzebo",
    "Halilovic","Handzic","Hasanovic","Hodzic","Hrnjic","Imamovic","Jovanovic","Juric","Kamber","Karic",
    "Kovacevic","Kurtovic","Lagumdzija","Mandic","Maric","Mehic","Mujanovic","Mustafic","Nikolic","Nuic",
    "Petrovic","Ramic","Selimovic","Softic","Sabanovic","Sehic","Tahirovic","Terzic","Vidovic","Zukic"
]

used_names = set()
def unique_name():
    while True:
        combo = (random.choice(NAMES), random.choice(SURNAMES))
        if combo not in used_names:
            used_names.add(combo)
            return combo

def guid():
    return str(uuid.uuid4())

# ── open DB ───────────────────────────────────────────────────────────────────
conn = sqlite3.connect(DB_PATH)
conn.execute("PRAGMA foreign_keys = OFF")
cur = conn.cursor()

# ── CLEAR ALL DATA ────────────────────────────────────────────────────────────
print("Clearing all data...")
tables_to_clear = [
    "TurnirParovi","Mecevi","Registracije","Notifikacije",
    "Pracenja","Turniri","Lige",
    "AspNetUserRoles","AspNetUserClaims","AspNetUserLogins",
    "AspNetUserTokens","AspNetUsers","AspNetRoleClaims","AspNetRoles",
]
for t in tables_to_clear:
    cur.execute(f"DELETE FROM [{t}]")
for t in ["TurnirParovi","Mecevi","Registracije","Notifikacije",
          "Pracenja","Turniri","Lige"]:
    cur.execute("DELETE FROM sqlite_sequence WHERE name=?", (t,))
conn.commit()
print("Database cleared.")

# ── ROLES ─────────────────────────────────────────────────────────────────────
print("Creating roles...")
roles = {"Administrator": guid(), "Organizator": guid(), "Korisnik": guid()}
for name, rid in roles.items():
    cur.execute(
        "INSERT INTO AspNetRoles(Id,Name,NormalizedName,ConcurrencyStamp) VALUES(?,?,?,?)",
        (rid, name, name.upper(), guid())
    )

# ── ADMIN / ORG USERS ─────────────────────────────────────────────────────────
print("Creating admin/org users...")
ADMIN_HASH = "AQAAAAIAAYagAAAAEGkBKkrfFEkSqpb3kNOVVvvp4N8yJVJnE+nMr9G0pMHwG+M8tZexample=="

def insert_user(uid, email, ime, prezime, pw_hash, role_names):
    norm_email = email.upper()
    cur.execute("""
        INSERT INTO AspNetUsers(
            Id, UserName, NormalizedUserName, Email, NormalizedEmail,
            EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
            PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled,
            AccessFailedCount, Ime, Prezime, Grad, DatumRodjenja, DatumRegistracije
        ) VALUES(?,?,?,?,?,1,?,?,?,0,0,1,0,?,?,?,?,?)
    """, (
        uid, email, norm_email, email, norm_email,
        pw_hash, guid(), guid(),
        ime, prezime, "Sarajevo",
        datetime(1990,1,1).isoformat(),
        datetime(2024,1,1).isoformat()
    ))
    for rn in role_names:
        cur.execute("INSERT INTO AspNetUserRoles(UserId,RoleId) VALUES(?,?)",
                    (uid, roles[rn]))

admin_id = guid()
insert_user(admin_id, "admin@epinpong.com", "Admin", "Babo",
            ADMIN_HASH, ["Administrator"])

org_id = guid()
insert_user(org_id, "organizator@epinpong.com", "Organizator", "Turnira",
            ADMIN_HASH, ["Organizator","Korisnik"])

# ── SLOBODAN USER ─────────────────────────────────────────────────────────────
slobodan_id = "SLOBODAN"
cur.execute("""
    INSERT INTO AspNetUsers(
        Id, UserName, NormalizedUserName, Email, NormalizedEmail,
        EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
        PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled,
        AccessFailedCount, Ime, Prezime, Grad, DatumRodjenja, DatumRegistracije
    ) VALUES(?, 'slobodan@epinpong.local', 'SLOBODAN@EPINPONG.LOCAL', 'slobodan@epinpong.local', 'SLOBODAN@EPINPONG.LOCAL',
             1, NULL, ?, ?, 0, 0, 1, 0, 'Slobodan', 'Prolaz', 'Sistem', '2000-01-01T00:00:00', '2024-01-01T00:00:00')
""", (slobodan_id, guid(), guid()))

# ── 40 GUEST PLAYERS ─────────────────────────────────────────────────────────
print("Creating 40 guest players...")
guest_ids = []
for i in range(40):
    ime, prezime = unique_name()
    uid = guid()
    email = f"gost{i+1:02d}@epinpong.local"
    norm_email = email.upper()
    cur.execute("""
        INSERT INTO AspNetUsers(
            Id, UserName, NormalizedUserName, Email, NormalizedEmail,
            EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
            PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled,
            AccessFailedCount, Ime, Prezime, Grad, DatumRodjenja, DatumRegistracije
        ) VALUES(?,?,?,?,?,1,NULL,?,?,0,0,1,0,?,?,?,?,?)
    """, (
        uid, email, norm_email, email, norm_email,
        guid(), guid(),
        ime, prezime, "Sarajevo",
        datetime(1995,1,1).isoformat(),
        datetime(2024,6,1).isoformat()
    ))
    guest_ids.append(uid)
    cur.execute("INSERT INTO AspNetUserRoles(UserId,RoleId) VALUES(?,?)",
                (uid, roles["Korisnik"]))

conn.commit()
print(f"Created {len(guest_ids)} guest players.")

# ── LEAGUE ────────────────────────────────────────────────────────────────────
print("Creating league...")
cur.execute("""
    INSERT INTO Lige(Naziv, Opis, Sezona, DatumPocetka)
    VALUES(?,?,?,?)
""", (
    "ePinPong Liga 2025",
    "Zvanična liga stonog tenisa za sezonu 2025.",
    "2025",
    datetime(2025,1,1).isoformat()
))
liga_id = cur.lastrowid
conn.commit()
print(f"League created (ID={liga_id}).")

# ── enum values ───────────────────────────────────────────────────────────────
TipMeca_GrupnaFaza = 0
TipMeca_Zavrsnica  = 1
StatusTurnira_UToku   = 2
StatusTurnira_Zavrsen = 3

def insert_mec(turnir_id, igrac1, igrac2, poeni1, poeni2, runda,
               tip, match_code, winner_next=None, loser_next=None,
               winner_slot=None, loser_slot=None, placing="",
               naziv_grupe=None, vrijeme=None):
    if vrijeme is None:
        vrijeme = datetime(2025,3,1,9,0).isoformat()
    cur.execute("""
        INSERT INTO Mecevi(
            TurnirID, Igrac1ID, Igrac2ID, Igrac1PartnerID, Igrac2PartnerID,
            PoeniIgrac1, PoeniIgrac2, VrijemeMeca, Runda, Odigran,
            TipMeca, MatchCode, WinnerNextMatchCode, LoserNextMatchCode,
            WinnerNextMatchSlot, LoserNextMatchSlot, PlacingRange, NazivGrupe
        ) VALUES(?,?,?,NULL,NULL,?,?,?,?,1,?,?,?,?,?,?,?,?)
    """, (
        turnir_id, igrac1, igrac2,
        poeni1, poeni2,
        vrijeme, runda,
        tip, match_code,
        winner_next, loser_next,
        winner_slot, loser_slot,
        placing, naziv_grupe
    ))

def add_reg(turnir_id, igrac_id, sesir=1):
    cur.execute("""
        INSERT INTO Registracije(TurnirID, KorisnikID, DatumRegistracije, Odobren, Sesir)
        VALUES(?,?,?,1,?)
    """, (turnir_id, igrac_id, datetime(2025,1,15).isoformat(), sesir))

# ═══════════════════════════════════════════════════════════════════════════════
# TOURNAMENT 1 – 29 Players (Group stage completed)
# ═══════════════════════════════════════════════════════════════════════════════
print("\nCreating Tournament 1 (29 players, group stage completed)...")

t29_players = guest_ids[:29]
random.shuffle(t29_players)

cur.execute("""
    INSERT INTO Turniri(
        Naziv, Status, DatumPocetka, DatumKraja, MaxIgraca,
        Lokacija, Opis, SlikaUrl, OrganizatorId, LigaID, Kolo,
        PobjednikID, DrugoplasiraniID, TrecaplasiraniID, TipTakmicenja, SistemTurnira
    ) VALUES(?,?,?,?,?,?,?,NULL,?,?,?,NULL,NULL,NULL,1,2)
""", (
    "Turnir 29 igrača (Završena grupna faza)",
    StatusTurnira_UToku,
    datetime(2025,5,10,9,0).isoformat(),
    datetime(2025,5,10,18,0).isoformat(),
    32, "Sarajevo", "Turnir od 29 igrača sa završenom grupnom fazom.",
    org_id, liga_id, 1
))
t29_id = cur.lastrowid

for i, pid in enumerate(t29_players):
    add_reg(t29_id, pid, (i % 4) + 1)

# 29 players: 5 groups of 4 + 3 groups of 3 = 8 groups total (A to H)
t29_groups = []
idx = 0
for g in range(5):
    t29_groups.append(t29_players[idx : idx + 4])
    idx += 4
for g in range(3):
    t29_groups.append(t29_players[idx : idx + 3])
    idx += 3

for g_idx, grp in enumerate(t29_groups):
    gname = f"Grupa {chr(65+g_idx)}"
    n = len(grp)
    m_idx = 1
    for i in range(n):
        for j in range(i + 1, n):
            # Random score (3:0, 3:1, 3:2)
            p1_win = random.choice([True, False])
            p1_score = 3 if p1_win else random.choice([0, 1, 2])
            p2_score = random.choice([0, 1, 2]) if p1_win else 3
            
            insert_mec(
                t29_id, grp[i], grp[j], p1_score, p2_score, 1,
                TipMeca_GrupnaFaza, f"T29_G{g_idx}_M{m_idx}",
                naziv_grupe=gname,
                vrijeme=datetime(2025,5,10,9,0).isoformat()
            )
            m_idx += 1

conn.commit()
print(f"Tournament 1 created (ID={t29_id}). 29 players, 8 groups, all group matches completed.")

# ═══════════════════════════════════════════════════════════════════════════════
# TOURNAMENT 2 – 11 Players (Group stage completed)
# ═══════════════════════════════════════════════════════════════════════════════
print("\nCreating Tournament 2 (11 players, group stage completed)...")

t11_players = guest_ids[29:40] # 11 players
random.shuffle(t11_players)

cur.execute("""
    INSERT INTO Turniri(
        Naziv, Status, DatumPocetka, DatumKraja, MaxIgraca,
        Lokacija, Opis, SlikaUrl, OrganizatorId, LigaID, Kolo,
        PobjednikID, DrugoplasiraniID, TrecaplasiraniID, TipTakmicenja, SistemTurnira
    ) VALUES(?,?,?,?,?,?,?,NULL,?,?,?,NULL,NULL,NULL,1,2)
""", (
    "Turnir 11 igrača (Završena grupna faza)",
    StatusTurnira_UToku,
    datetime(2025,6,15,9,0).isoformat(),
    datetime(2025,6,15,18,0).isoformat(),
    16, "Mostar", "Turnir od 11 igrača sa završenom grupnom fazom.",
    org_id, liga_id, 2
))
t11_id = cur.lastrowid

for i, pid in enumerate(t11_players):
    add_reg(t11_id, pid, (i % 4) + 1)

# 11 players: 2 groups of 4 + 1 group of 3 = 3 groups total (A, B, C)
t11_groups = [
    t11_players[0:4],
    t11_players[4:8],
    t11_players[8:11]
]

for g_idx, grp in enumerate(t11_groups):
    gname = f"Grupa {chr(65+g_idx)}"
    n = len(grp)
    m_idx = 1
    for i in range(n):
        for j in range(i + 1, n):
            p1_win = random.choice([True, False])
            p1_score = 3 if p1_win else random.choice([0, 1, 2])
            p2_score = random.choice([0, 1, 2]) if p1_win else 3
            
            insert_mec(
                t11_id, grp[i], grp[j], p1_score, p2_score, 1,
                TipMeca_GrupnaFaza, f"T11_G{g_idx}_M{m_idx}",
                naziv_grupe=gname,
                vrijeme=datetime(2025,6,15,9,0).isoformat()
            )
            m_idx += 1

conn.commit()
print(f"Tournament 2 created (ID={t11_id}). 11 players, 3 groups, all group matches completed.")

# ── SAVE BACKUP AND COPIES ────────────────────────────────────────────────────
conn.close()

shutil.copy2(DB_PATH, BACKUP_PATH)
shutil.copy2(DB_PATH, SEEDED_29_PATH)

print("\n=== DONE ===")
print(f"League ID:        {liga_id}")
print(f"Tournament 1:     ID={t29_id} (in progress, 29 players, group stage done)")
print(f"Tournament 2:     ID={t11_id} (in progress, 11 players, group stage done)")
print(f"Total Players:    {len(guest_ids)}")
print(f"Database files:   {DB_PATH}, {SEEDED_29_PATH}, {BACKUP_PATH}")
