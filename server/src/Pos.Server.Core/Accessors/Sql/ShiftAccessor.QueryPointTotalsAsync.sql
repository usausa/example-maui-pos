SELECT
    COALESCE(SUM(PointsEarned), 0) AS Earned,
    COALESCE(SUM(PointsRedeemed), 0) AS Redeemed
FROM Transactions
WHERE ShiftId = /*@ shiftId */'' AND Status = 'Completed'
