import sqlite3

conn = sqlite3.connect('epinpong.db')
cur = conn.cursor()

# Provjeri postojeće kolone
cols = [row[1] for row in cur.execute("PRAGMA table_info(Lige);")]
print("Postojece kolone u Lige:", cols)

# Dodaj OrganizatorId ako ne postoji
if 'OrganizatorId' not in cols:
    cur.execute("ALTER TABLE Lige ADD COLUMN OrganizatorId TEXT NULL;")
    print("Dodana kolona OrganizatorId u tabelu Lige.")
else:
    print("Kolona OrganizatorId vec postoji.")

conn.commit()
conn.close()
print("Gotovo.")
