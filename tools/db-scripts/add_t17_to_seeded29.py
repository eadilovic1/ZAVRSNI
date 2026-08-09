import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import sqlite3, random
from datetime import datetime

db_files = ['epinpong_seeded_29.db', 'epinpong.db', 'epinpong_seed_backup.db']
if len(sys.argv) > 1:
    db_files = [sys.argv[1]]

for db_path in db_files:
    print(f"\n==========================================")
    print(f"Processing database: {db_path}")
    print(f"==========================================")
    
    random.seed(999)

    conn = sqlite3.connect(db_path)
    conn.execute("PRAGMA foreign_keys = OFF")
    cur = conn.cursor()

    # 1. Osiguraj da kolone i tabele postoje
    cur.execute("PRAGMA table_info(Turniri)")
    t_cols = [r[1] for r in cur.fetchall()]
    if 'TipTakmicenja' not in t_cols:
        cur.execute("ALTER TABLE Turniri ADD COLUMN TipTakmicenja INTEGER NOT NULL DEFAULT 1")
    if 'SistemTurnira' not in t_cols:
        cur.execute("ALTER TABLE Turniri ADD COLUMN SistemTurnira INTEGER NOT NULL DEFAULT 3")

    cur.execute("PRAGMA table_info(Registracije)")
    r_cols = [r[1] for r in cur.fetchall()]
    if 'Sesir' not in r_cols:
        cur.execute("ALTER TABLE Registracije ADD COLUMN Sesir INTEGER NOT NULL DEFAULT 1")

    cur.execute("PRAGMA table_info(Mecevi)")
    m_cols = [r[1] for r in cur.fetchall()]
    if 'Igrac1PartnerID' not in m_cols:
        cur.execute("ALTER TABLE Mecevi ADD COLUMN Igrac1PartnerID TEXT")
    if 'Igrac2PartnerID' not in m_cols:
        cur.execute("ALTER TABLE Mecevi ADD COLUMN Igrac2PartnerID TEXT")

    cur.execute("""
        CREATE TABLE IF NOT EXISTS TurnirParovi (
            ID INTEGER PRIMARY KEY AUTOINCREMENT,
            TurnirID INTEGER NOT NULL,
            Igrac1ID TEXT NOT NULL,
            Igrac2ID TEXT NOT NULL,
            DatumPrijave TEXT NOT NULL,
            FOREIGN KEY (TurnirID) REFERENCES Turniri(ID) ON DELETE CASCADE,
            FOREIGN KEY (Igrac1ID) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT,
            FOREIGN KEY (Igrac2ID) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT
        )
    """)
    conn.commit()

    # 2. Obrisati prethodni turnir sa 17 igraca ako vec postoji
    cur.execute("SELECT ID FROM Turniri WHERE Naziv LIKE '%17 igra%'")
    old_ids = [r[0] for r in cur.fetchall()]
    for old_id in old_ids:
        cur.execute("DELETE FROM Mecevi WHERE TurnirID=?", (old_id,))
        cur.execute("DELETE FROM Registracije WHERE TurnirID=?", (old_id,))
        cur.execute("DELETE FROM TurnirParovi WHERE TurnirID=?", (old_id,))
        cur.execute("DELETE FROM Turniri WHERE ID=?", (old_id,))
        print(f"Obrisan stari turnir ID={old_id}")
    conn.commit()

    # 3. Ucitaj podatke iz prvog turnira
    cur.execute("SELECT ID FROM Turniri ORDER BY ID LIMIT 1")
    first_t_id = cur.fetchone()[0]

    cur.execute("SELECT KorisnikID FROM Registracije WHERE TurnirID=?", (first_t_id,))
    all_player_ids = [r[0] for r in cur.fetchall()]

    cur.execute("SELECT OrganizatorId FROM Turniri WHERE ID=?", (first_t_id,))
    org_id = cur.fetchone()[0]

    cur.execute("SELECT LigaID FROM Turniri WHERE ID=?", (first_t_id,))
    liga_id = cur.fetchone()[0]

    # Izaberi 17 nasumicnih igraca
    t17_players = random.sample(all_player_ids, 17)

    TipMeca_GrupnaFaza = 0
    StatusTurnira_UToku = 2
    SistemTurnira_DoubleEliminationUtjesni = 3

    # Kreiraj turnir sa 17 igraca
    cur.execute("""
        INSERT INTO Turniri(
            Naziv, Status, DatumPocetka, DatumKraja, MaxIgraca,
            Lokacija, Opis, SlikaUrl, OrganizatorId, LigaID, Kolo,
            PobjednikID, DrugoplasiraniID, TrecaplasiraniID, TipTakmicenja, SistemTurnira
        ) VALUES(?,?,?,?,?,?,?,NULL,?,?,?,NULL,NULL,NULL,1,?)
    """, (
        "Testni turnir 17 igraca (Zavrsena grupna faza)",
        StatusTurnira_UToku,
        datetime(2026, 9, 1, 9, 0).isoformat(),
        datetime(2026, 9, 1, 18, 0).isoformat(),
        32, "Sportska Dvorana Zenica",
        "Turnir od 17 igraca sa zavrsenom grupnom fazom za testiranje plasmana.",
        org_id, liga_id, 3,
        SistemTurnira_DoubleEliminationUtjesni
    ))
    t17_id = cur.lastrowid
    print(f"Kreiran novi turnir ID={t17_id}")

    # Registriraj igrace
    for i, pid in enumerate(t17_players):
        cur.execute("""
            INSERT INTO Registracije(TurnirID, KorisnikID, DatumRegistracije, Odobren, Sesir)
            VALUES(?,?,?,1,?)
        """, (t17_id, pid, datetime(2026, 8, 1).isoformat(), (i % 4) + 1))

    # 17 igraca -> 5 grupa (A: 4, B: 4, C: 3, D: 3, E: 3)
    # Ukupno: 4 + 4 + 3 + 3 + 3 = 17 igraca. Max velicina grupe je 4, nema grupa od 5!
    random.shuffle(t17_players)
    groups = [
        t17_players[0:4],    # Grupa A - 4 igraca
        t17_players[4:8],    # Grupa B - 4 igraca
        t17_players[8:11],   # Grupa C - 3 igraca
        t17_players[11:14],  # Grupa D - 3 igraca
        t17_players[14:17],  # Grupa E - 3 igraca
    ]

    def rand_result():
        winner = random.choice([1, 2])
        loser_score = random.choice([0, 1, 2])
        if winner == 1:
            return (3, loser_score)
        else:
            return (loser_score, 3)

    total_matches = 0
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
                    t17_id, p1, p2,
                    p1_score, p2_score,
                    datetime(2026, 9, 1, 9, 0).isoformat(),
                    TipMeca_GrupnaFaza,
                    f"T17_G{g_idx}_M{m_idx}",
                    gname
                ))
                m_idx += 1
                total_matches += 1

    conn.commit()

    cur.execute("SELECT COUNT(*) FROM Mecevi WHERE TurnirID=?", (t17_id,))
    mec_count = cur.fetchone()[0]
    cur.execute("SELECT COUNT(*) FROM Registracije WHERE TurnirID=?", (t17_id,))
    reg_count = cur.fetchone()[0]

    print(f"Registrations: {reg_count} (expected: 17)")
    print(f"Matches: {mec_count} (expected: 21)")
    print(f"Groups: A(4), B(4), C(3), D(3), E(3)")
    print(f"DONE - Tournament ID={t17_id} updated in {db_path}")

    conn.close()
