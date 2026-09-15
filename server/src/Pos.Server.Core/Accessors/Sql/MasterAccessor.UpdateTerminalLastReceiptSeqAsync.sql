UPDATE
    Terminals
SET
    LastReceiptSeq = MAX(LastReceiptSeq, /*@ receiptSeq */0),
    LastSeenAt = /*@ seenAt */''
WHERE
    Id = /*@ id */''
