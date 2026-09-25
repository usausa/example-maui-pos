INSERT INTO
    OrderDeposits
    (Id, OrderId, StoreId, TerminalId, ShiftId, StaffId, Type, PaymentMethodId, Kind, Amount, Reference, OccurredAt, CreatedAt)
SELECT
    /*@ id */'',
    o.Id,
    o.StoreId,
    /*@ terminalId */'',
    /*@ shiftId */'',
    /*@ staffId */'',
    /*@ type */'',
    /*@ paymentMethodId */'',
    /*@ kind */'',
    /*@ amount */0,
    /*@ reference */'',
    /*@ occurredAt */'',
    /*@ createdAt */''
FROM
    Orders o
WHERE
    o.Id = /*@ orderId */''
    AND o.Status IN ('Ordered', 'Arrived')
    AND COALESCE((
        SELECT
            SUM(CASE WHEN d.Type = 'Receive' THEN d.Amount ELSE -d.Amount END)
        FROM
            OrderDeposits d
        WHERE
            d.OrderId = o.Id
    ), 0) = CAST(/*@ balance */0 AS NUMERIC)
    AND EXISTS (
        SELECT
            1
        FROM
            Shifts s
        WHERE
            s.Id = /*@ shiftId */''
            AND s.Status = 'Open'
    )
RETURNING
    *
