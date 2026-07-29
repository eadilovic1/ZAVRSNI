import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import sqlite3, random
from datetime import datetime

DB_PATH = 'epinpong_seeded_29.db'
random.seed(777)

conn = sqlite3.connect(DB_PATH)
conn.execute("PRAGMA foreign_keys = OFF")
cur = conn.cursor()

# --- Ucitaj postojece podatke ---
cur.execute("SELECT KorisnikID FROM Registracije WHERE TurnirID=9")
all_player_ids = [r[0] for r in cur.fetchall()]

cur.execute("SELECT OrganizatorId FROM Turniri WHERE ID=9")
org_id = cur.fetchone()[0]

cur.execute("SELECT LigaID FROM Turniri WHERE ID=9")
liga_id = cur.fetchone()[0]

# --- Izaberi nasumicnih 11 igraca od postojecih 29 ---
t11_players = random.sample(all_player_ids, 11)
print(f"Selected 11 players: {len(t11_players)}")

# --- Enum vrijednosti ---
TipMeca_GrupnaFaza = 0
StatusTurnira_UToku = 2

# --- Kreiraj turnir sa 11 igraca ---
cur.execute("""
    INSERT INTO Turniri(
        Naziv, Status, DatumPocetka, DatumKraja, MaxIgraca,
        Lokacija, Opis, SlikaUrl, OrganizatorId, LigaID, Kolo,
        PobjednikID, DrugoplasiraniID, TrecaplasiraniID
    ) VALUES(?,?,?,?,?,?,?,?,?,?,?,NULL,NULL,NULL)
""", (
    'Testni turnir 11 igraca (Zavrsena grupna faza)',
    StatusTurnira_UToku,
    datetime(2026, 8, 1, 9, 0).isoformat(),
    datetime(2026, 8, 1, 18, 0).isoformat(),
    16, 'Stolnoteniska Dvorana',
    'Turnir od 11 igraca sa zavrsenom grupnom fazom za testiranje.',
    'https://images.unsplash.com/photo-1534158914592-062992fbe900?q=80&w=1200&auto=format&fit=crop',
    org_id, liga_id, 2
))
t11_id = cur.lastrowid
print(f"Created tournament ID={t11_id}")

# --- Registriraj igrace ---
for pid in t11_players:
    cur.execute("""
        INSERT INTO Registracije(TurnirID, KorisnikID, DatumRegistracije, Odobren)
        VALUES(?,?,?,1)
    """, (t11_id, pid, datetime(2026, 7, 1).isoformat()))

# --- Format grupa: 11 igraca -> 2 grupe od 4 + 1 grupa od 3 ---
# Nasumicno rasporedi igrace u grupe
random.shuffle(t11_players)
groups = [
    t11_players[0:4],   # Grupa A - 4 igraca
    t11_players[4:8],   # Grupa B - 4 igraca
    t11_players[8:11],  # Grupa C - 3 igraca
]

def rand_result():
    """Vraca (poeni1, poeni2) gdje pobjednik ima 3, a gubitnik 0/1/2"""
    winner = random.choice([1, 2])
    loser_score = random.choice([0, 1, 2])
    if winner == 1:
        return (3, loser_score)
    else:
        return (loser_score, 3)

for g_idx, grp in enumerate(groups):
    gname = f"Grupa {chr(65 + g_idx)}"
    n = len(grp)
    m_idx = 1
    for i in range(n):
        for j in range(i + 1, n):
            p1, p2 = grp[i], grp[j]
            p1_score, p2_score = rand_result()
            cur.execute("""
                INSERT INTO Mecevi(
                    TurnirID, Igrac1ID, Igrac2ID,
                    PoeniIgrac1, PoeniIgrac2,
                    VrijemeMeca, Runda, Odigran,
                    TipMeca, MatchCode, WinnerNextMatchCode, LoserNextMatchCode,
                    WinnerNextMatchSlot, LoserNextMatchSlot, PlacingRange, NazivGrupe
                ) VALUES(?,?,?,?,?,?,1,1,?,?,NULL,NULL,NULL,NULL,'',?)
            """, (
                t11_id, p1, p2,
                p1_score, p2_score,
                datetime(2026, 8, 1, 9, 0).isoformat(),
                TipMeca_GrupnaFaza,
                f"T11_G{g_idx}_M{m_idx}",
                gname
            ))
            m_idx += 1

conn.commit()

# --- Verifikacija ---
cur.execute("SELECT COUNT(*) FROM Mecevi WHERE TurnirID=?", (t11_id,))
mec_count = cur.fetchone()[0]
cur.execute("SELECT COUNT(*) FROM Registracije WHERE TurnirID=?", (t11_id,))
reg_count = cur.fetchone()[0]

print(f"Verification: {reg_count} registrations, {mec_count} matches created")
print(f"Groups: A(4), B(4), C(3)")
print(f"Expected matches: 6+6+3 = 15 -> actual: {mec_count}")
print(f"\nDONE - Tournament ID={t11_id} added to {DB_PATH}")

conn.close()
