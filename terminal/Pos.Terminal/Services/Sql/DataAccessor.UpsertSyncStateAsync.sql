INSERT INTO
    SyncState (Key, Value)
VALUES
    (/*@ key */'', /*@ value */'')
ON CONFLICT (Key) DO UPDATE SET
    Value = excluded.Value
