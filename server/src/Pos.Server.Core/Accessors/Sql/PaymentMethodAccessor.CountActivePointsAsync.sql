SELECT COUNT(*) FROM PaymentMethods WHERE Kind = 'Points' AND IsActive = 1 AND IsDeleted = 0 AND Id <> /*@ exceptId */''
