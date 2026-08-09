import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
import sqlite3

conn = sqlite3.connect('epinpong_seeded_29.db')
cur = conn.cursor()

# Show Mecevi columns
cur.execute("PRAGMA table_info(Mecevi)")
cols = [r[1] for r in cur.fetchall()]
print('Mecevi columns:', cols)

# Show Registracije for the existing tournament
cur.execute("SELECT KorisnikID FROM Registracije WHERE TurnirID=9")
reg_ids = [r[0] for r in cur.fetchall()]
print(f'\nRegistered player IDs for T9 ({len(reg_ids)} total):')
for rid in reg_ids:
    print(f'  {rid}')

# Show sample match from T9 to understand format
cur.execute("SELECT * FROM Mecevi WHERE TurnirID=9 LIMIT 2")
rows = cur.fetchall()
print(f'\nSample matches from T9:')
for r in rows:
    print(f'  {r}')

# Registracije columns
cur.execute("PRAGMA table_info(Registracije)")
rcols = [r[1] for r in cur.fetchall()]
print('\nRegistracije columns:', rcols)

conn.close()
