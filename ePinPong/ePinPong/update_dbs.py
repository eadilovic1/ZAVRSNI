import sqlite3

dbs = ['epinpong.db', 'epinpong_prazno.db', 'epinpong_seed_backup.db', 'epinpong_seeded_29.db']

for db in dbs:
    try:
        conn = sqlite3.connect(db)
        cur = conn.cursor()
        cols = [row[1] for row in cur.execute("PRAGMA table_info(Lige);")]
        if 'OrganizatorId' not in cols:
            cur.execute("ALTER TABLE Lige ADD COLUMN OrganizatorId TEXT NULL;")
            conn.commit()
            print(f"Successfully added OrganizatorId to {db}")
        else:
            print(f"OrganizatorId already present in {db}")
        conn.close()
    except Exception as e:
        print(f"Error processing {db}: {e}")
