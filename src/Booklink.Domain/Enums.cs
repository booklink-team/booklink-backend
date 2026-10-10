namespace Booklink.Domain;

public enum UserRole { Student, Librarian }

public enum CopyState { Available, Reserved, Issued }

public enum ReservationStatus { Active, Cancelled, Expired, Completed }

public enum LoanStatus { Active, Returned }
