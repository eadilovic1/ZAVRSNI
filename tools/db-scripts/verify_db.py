import sqlite3
conn = sqlite3.connect('epinpong.db')
cur = conn.cursor()

cur.execute("SELECT COUNT(*) FROM AspNetUsers WHERE Email LIKE '%@epinpong.local'")
print('Guest players:', cur.fetchone()[0])

cur.execute("SELECT ID, Naziv, Status, MaxIgraca, PobjednikID IS NOT NULL FROM Turniri")
status_map = {0:'Planiran',1:'Aktivan',2:'UToku',3:'Zavrsen',4:'Otkazan'}
for r in cur.fetchall():
    print(f"  Turnir {r[0]}: {r[1]} | Status={status_map.get(r[2],r[2])} | Max={r[3]} | HasWinner={bool(r[4])}")

cur.execute("SELECT TurnirID, COUNT(*) FROM Mecevi GROUP BY TurnirID")
print("Matches per tournament:", dict(cur.fetchall()))

cur.execute("SELECT TurnirID, COUNT(*) FROM TurnirParovi GROUP BY TurnirID")
print("Pairs per tournament:", dict(cur.fetchall()))

cur.execute("SELECT TurnirID, COUNT(*) FROM Registracije GROUP BY TurnirID")
print("Registrations per tournament:", dict(cur.fetchall()))

cur.execute("SELECT COUNT(*) FROM Lige")
print("Leagues:", cur.fetchone()[0])

conn.close()
print("Verification complete.")
