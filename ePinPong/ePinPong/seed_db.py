import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import sqlite3, uuid, random, shutil
from datetime import datetime

DB_PATH = "epinpong.db"
BACKUP_PATH = "epinpong_seed_backup.db"

random.seed(42)

NAMES = [
    "Adnan","Amar","Armin","Adin","Aldin","Almir","Damir","Denis","Dino","Edin",
    "Eldin","Elmir","Emir","Enes","Ermin","Faruk","Haris","Jasmin","Kenan","Luka",
    "Mahir","Mario","Marko","Mirza","Muamer","Muris","Ned","Nermin","Niko","Omar",
    "Petar","Rasim","Samir","Sanel","Senad","Tarik","Tomislav","Vedran","Zlatan","Zoran"
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

# ── 32 GUEST PLAYERS ─────────────────────────────────────────────────────────
print("Creating 32 guest players...")
guest_ids = []
for i in range(32):
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

# ── enum values (stored as integers in SQLite) ────────────────────────────────
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
# TOURNAMENT 1 – Fully completed, 32 players
# ═══════════════════════════════════════════════════════════════════════════════
print("\nCreating Tournament 1 (completed, 32 players)...")

t1_players = guest_ids[:]
random.shuffle(t1_players)

cur.execute("""
    INSERT INTO Turniri(
        Naziv, Status, DatumPocetka, DatumKraja, MaxIgraca,
        Lokacija, Opis, SlikaUrl, OrganizatorId, LigaID, Kolo,
        PobjednikID, DrugoplasiraniID, TrecaplasiraniID
    ) VALUES(?,?,?,?,?,?,?,NULL,?,?,?,?,?,?)
""", (
    "Zimski Kup 2025",
    StatusTurnira_Zavrsen,
    datetime(2025,1,26,9,30).isoformat(),
    datetime(2025,1,26,15,0).isoformat(),
    32,"Sarajevo","Prvo kolo - zimski kup.",
    org_id, liga_id, 1,
    t1_players[0], t1_players[1], t1_players[2]
))
t1_id = cur.lastrowid

for i, pid in enumerate(t1_players):
    sesir = 1 if i < 8 else (2 if i < 16 else (3 if i < 24 else 4))
    add_reg(t1_id, pid, sesir)

# Group stage: 8 groups of 4
t1_groups = [t1_players[i*4:(i+1)*4] for i in range(8)]
t1_advancing = []  # top 2 from each group → 16 players

for g_idx, grp in enumerate(t1_groups):
    gname = f"Grupa {chr(65+g_idx)}"
    scores = [0,0,0,0]
    for p1i,p2i in [(0,1),(0,2),(0,3),(1,2),(1,3),(2,3)]:
        scores[p1i] += 3
        insert_mec(t1_id, grp[p1i], grp[p2i], 3, 1, 1,
                   TipMeca_GrupnaFaza, f"T1_G{g_idx}_{p1i}{p2i}",
                   naziv_grupe=gname,
                   vrijeme=datetime(2025,1,26,9,0).isoformat())
    ranked = sorted(range(4), key=lambda x: -scores[x])
    t1_advancing.append(grp[ranked[0]])
    t1_advancing.append(grp[ranked[1]])

# Knockout: R16 → QF → SF → 3rd place + Final
# R16 (8 matches)
r16_w = []
for i in range(0,16,2):
    p1,p2 = t1_advancing[i], t1_advancing[i+1]
    insert_mec(t1_id, p1, p2, 3, 1, 2,
               TipMeca_Zavrsnica, f"T1_R16_{i//2}",
               placing="17-32",
               vrijeme=datetime(2025,1,26,11,0).isoformat())
    r16_w.append(p1)

# QF (4 matches)
qf_w = []; qf_l = []
for i in range(0,8,2):
    p1,p2 = r16_w[i], r16_w[i+1]
    insert_mec(t1_id, p1, p2, 3, 2, 3,
               TipMeca_Zavrsnica, f"T1_QF_{i//2}",
               placing="5-8",
               vrijeme=datetime(2025,1,26,12,0).isoformat())
    qf_w.append(p1); qf_l.append(p2)

# SF (2 matches)
sf_w = []; sf_l = []
for i in range(0,4,2):
    p1,p2 = qf_w[i], qf_w[i+1]
    insert_mec(t1_id, p1, p2, 3, 2, 4,
               TipMeca_Zavrsnica, f"T1_SF_{i//2}",
               placing="3-4",
               vrijeme=datetime(2025,1,26,13,0).isoformat())
    sf_w.append(p1); sf_l.append(p2)

# 3rd place match
insert_mec(t1_id, sf_l[0], sf_l[1], 3, 2, 5,
           TipMeca_Zavrsnica, "T1_3PL",
           placing="3-4",
           vrijeme=datetime(2025,1,26,14,0).isoformat())

# Final
insert_mec(t1_id, sf_w[0], sf_w[1], 3, 1, 5,
           TipMeca_Zavrsnica, "T1_F",
           placing="1-2",
           vrijeme=datetime(2025,1,26,14,30).isoformat())

conn.commit()
print(f"Tournament 1 created (ID={t1_id}).")

# ═══════════════════════════════════════════════════════════════════════════════
# TOURNAMENT 2 – 16 players, group stage done, pairs FORMED, no pair matches
# ═══════════════════════════════════════════════════════════════════════════════
print("\nCreating Tournament 2 (group stage done, pairs formed, no pair matches)...")

t2_players = guest_ids[:16]
random.shuffle(t2_players)

cur.execute("""
    INSERT INTO Turniri(
        Naziv, Status, DatumPocetka, DatumKraja, MaxIgraca,
        Lokacija, Opis, SlikaUrl, OrganizatorId, LigaID, Kolo,
        PobjednikID, DrugoplasiraniID, TrecaplasiraniID
    ) VALUES(?,?,?,?,?,?,?,NULL,?,?,?,NULL,NULL,NULL)
""", (
    "Proljetni Kup 2025",
    StatusTurnira_UToku,
    datetime(2025,3,30,9,30).isoformat(),
    datetime(2025,3,30,15,0).isoformat(),
    16,"Mostar","Drugo kolo - proljetni kup.",
    org_id, liga_id, 2
))
t2_id = cur.lastrowid

for i, pid in enumerate(t2_players):
    sesir = 1 if i < 4 else (2 if i < 8 else (3 if i < 12 else 4))
    add_reg(t2_id, pid, sesir)

# 4 groups of 4
t2_groups = [t2_players[i*4:(i+1)*4] for i in range(4)]
t2_gw = []
for g_idx, grp in enumerate(t2_groups):
    gname = f"Grupa {chr(65+g_idx)}"
    scores = [0,0,0,0]
    for p1i,p2i in [(0,1),(0,2),(0,3),(1,2),(1,3),(2,3)]:
        scores[p1i] += 3
        insert_mec(t2_id, grp[p1i], grp[p2i], 3, 1, 1,
                   TipMeca_GrupnaFaza, f"T2_G{g_idx}_{p1i}{p2i}",
                   naziv_grupe=gname,
                   vrijeme=datetime(2025,3,30,9,0).isoformat())
    ranked = sorted(range(4), key=lambda x: -scores[x])
    t2_gw.append([grp[ranked[0]], grp[ranked[1]]])

# Form 4 pairs (TurnirParovi) — no matches generated
pairs = [
    (t2_gw[0][0], t2_gw[1][1]),
    (t2_gw[1][0], t2_gw[0][1]),
    (t2_gw[2][0], t2_gw[3][1]),
    (t2_gw[3][0], t2_gw[2][1]),
]
for i1, i2 in pairs:
    cur.execute("""
        INSERT INTO TurnirParovi(TurnirID, Igrac1ID, Igrac2ID, DatumPrijave)
        VALUES(?,?,?,?)
    """, (t2_id, i1, i2, datetime(2025,3,30,11,0).isoformat()))

conn.commit()
print(f"Tournament 2 created (ID={t2_id}). {len(pairs)} pairs formed, no pair matches.")

# ═══════════════════════════════════════════════════════════════════════════════
# TOURNAMENT 3 – 16 players, group stage done, pairs NOT formed
# ═══════════════════════════════════════════════════════════════════════════════
print("\nCreating Tournament 3 (group stage done, pairs NOT formed)...")

t3_players = guest_ids[16:]
random.shuffle(t3_players)

cur.execute("""
    INSERT INTO Turniri(
        Naziv, Status, DatumPocetka, DatumKraja, MaxIgraca,
        Lokacija, Opis, SlikaUrl, OrganizatorId, LigaID, Kolo,
        PobjednikID, DrugoplasiraniID, TrecaplasiraniID
    ) VALUES(?,?,?,?,?,?,?,NULL,?,?,?,NULL,NULL,NULL)
""", (
    "Ljetni Kup 2025",
    StatusTurnira_UToku,
    datetime(2025,6,29,9,30).isoformat(),
    datetime(2025,6,29,15,0).isoformat(),
    16,"Banja Luka","Trece kolo - ljetni kup.",
    org_id, liga_id, 3
))
t3_id = cur.lastrowid

for i, pid in enumerate(t3_players):
    sesir = 1 if i < 4 else (2 if i < 8 else (3 if i < 12 else 4))
    add_reg(t3_id, pid, sesir)

# 4 groups of 4 — group stage matches played
t3_groups = [t3_players[i*4:(i+1)*4] for i in range(4)]
for g_idx, grp in enumerate(t3_groups):
    gname = f"Grupa {chr(65+g_idx)}"
    for p1i,p2i in [(0,1),(0,2),(0,3),(1,2),(1,3),(2,3)]:
        insert_mec(t3_id, grp[p1i], grp[p2i], 3, 1, 1,
                   TipMeca_GrupnaFaza, f"T3_G{g_idx}_{p1i}{p2i}",
                   naziv_grupe=gname,
                   vrijeme=datetime(2025,6,29,9,0).isoformat())

# NO TurnirParovi for T3
conn.commit()
print(f"Tournament 3 created (ID={t3_id}). No pairs formed.")

# ── SAVE BACKUP ───────────────────────────────────────────────────────────────
conn.close()
import shutil
shutil.copy2(DB_PATH, BACKUP_PATH)

print("\n=== DONE ===")
print(f"League ID:      {liga_id}")
print(f"Tournament 1:   ID={t1_id} (completed, 32 players)")
print(f"Tournament 2:   ID={t2_id} (in progress, 16 players, pairs formed)")
print(f"Tournament 3:   ID={t3_id} (in progress, 16 players, no pairs)")
print(f"Guest players:  {len(guest_ids)}")
print(f"\nBackup saved: {BACKUP_PATH}")
print("To restore: copy epinpong_seed_backup.db -> epinpong.db")
