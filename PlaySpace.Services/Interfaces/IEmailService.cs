using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends welcome email to new users
    /// </summary>
    Task SendWelcomeEmailAsync(string email, string name);

    /// <summary>
    /// Sends email verification link to new users
    /// </summary>
    Task SendEmailVerificationAsync(string email, string name, string webUrl, string deepLinkUrl);
    
    /// <summary>
    /// Sends business profile creation confirmation email
    /// </summary>
    Task SendBusinessProfileCreatedEmailAsync(BusinessProfileDto profile);
    
    /// <summary>
    /// Sends trainer profile creation confirmation email
    /// </summary>
    Task SendTrainerProfileCreatedEmailAsync(TrainerProfileDto profile);
    
    /// <summary>
    /// Sends TPay registration success notification
    /// </summary>
    Task SendTPayRegistrationSuccessEmailAsync(string email, string name, string merchantId, string activationLink);
    
    /// <summary>
    /// Sends TPay registration failure notification
    /// </summary>
    Task SendTPayRegistrationFailureEmailAsync(string email, string name, string errorMessage);
    
    /// <summary>
    /// Sends reservation confirmation email
    /// </summary>
    Task SendReservationConfirmationEmailAsync(ReservationDto reservation);
    
    /// <summary>
    /// Sends training session reminder email
    /// </summary>
    Task SendTrainingReminderEmailAsync(TrainingDto training, string participantEmail);
    
    /// <summary>
    /// Sends password reset email
    /// </summary>
    Task SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl);
    
    /// <summary>
    /// Sends generic notification email
    /// </summary>
    Task SendNotificationEmailAsync(string email, string subject, string body, bool isHtml = true);
    
    // Reservation Events
    Task SendReservationCreatedEmailAsync(ReservationDto reservation, string customerEmail, string customerName);
    Task SendReservationCancelledEmailAsync(ReservationDto reservation, string customerEmail, string customerName);
    Task SendReservationCancelledNoRefundEmailAsync(ReservationDto reservation, string customerEmail, string customerName, BusinessProfileDto business);
    Task SendReservationCancelledWithRefundEmailAsync(ReservationDto reservation, string customerEmail, string customerName, decimal refundAmount, decimal refundFee);
    Task SendReservationReminderEmailAsync(ReservationDto reservation, string customerEmail, string customerName);
    Task SendNewReservationNotificationEmailAsync(ReservationDto reservation, BusinessProfileDto business);
    Task SendNewReservationNotificationToParentEmailAsync(ReservationDto reservation, BusinessProfileDto parentBusiness, BusinessProfileDto childBusiness);

    // Partial Slot Cancellation Events
    Task SendPartialCancellationWithRefundEmailAsync(Guid reservationId, string facilityName, DateTime reservationDate, List<string> cancelledSlots, List<string> remainingSlots, decimal cancelledAmount, decimal refundAmount, decimal refundFee, string customerEmail, string customerName);
    Task SendPartialCancellationNoRefundEmailAsync(Guid reservationId, string facilityName, DateTime reservationDate, List<string> cancelledSlots, List<string> remainingSlots, decimal cancelledAmount, string customerEmail, string customerName, BusinessProfileDto business, string reason);
    
    // Training Events  
    Task SendTrainingBookedEmailAsync(TrainingDto training, string participantEmail, string participantName);
    Task SendTrainingCancelledEmailAsync(TrainingDto training, string participantEmail, string participantName);
    Task SendTrainingReminderEmailAsync(TrainingDto training, string participantEmail, string participantName);
    Task SendNewTrainingBookingEmailAsync(TrainingDto training, TrainerProfileDto trainer);
    
    // Payment Events
    Task SendPaymentSuccessfulEmailAsync(PaymentDto payment, string customerEmail, string customerName);
    Task SendPaymentFailedEmailAsync(string customerEmail, string customerName, string errorMessage, decimal amount);
    Task SendPaymentReceivedEmailAsync(PaymentDto payment, string providerEmail, string providerName);
    
    // Account Events
    Task SendPasswordChangedEmailAsync(string email, string name);
    Task SendEmailChangedNotificationEmailAsync(string oldEmail, string newEmail, string name);
    Task SendAccountDeletedEmailAsync(string email, string name, string? reason);

    // TPay Events
    Task SendTPayVerificationCompleteEmailAsync(string email, string name, string verificationStatus);

    // Agent Events
    Task SendAgentInvitationEmailAsync(string email, string subject, string htmlBody);

    // Trainer-Business Association Events
    /// <summary>
    /// Sends email to business profile owner requesting confirmation of trainer association.
    /// Links to a frontend page where business can choose permissions and confirm/reject.
    /// </summary>
    Task SendTrainerAssociationRequestEmailAsync(
        string businessEmail,
        string businessName,
        string trainerName,
        string trainerEmail,
        string confirmationPageUrl);

    /// <summary>
    /// Sends email to trainer when business confirms the association
    /// </summary>
    Task SendTrainerAssociationConfirmedEmailAsync(
        string trainerEmail,
        string trainerName,
        string businessName);

    /// <summary>
    /// Sends email to trainer when business rejects the association
    /// </summary>
    Task SendTrainerAssociationRejectedEmailAsync(
        string trainerEmail,
        string trainerName,
        string businessName,
        string? rejectionReason);

    /// <summary>
    /// Sends email to trainer when business removes the association
    /// </summary>
    Task SendAssociationRemovedEmailAsync(
        string trainerEmail,
        string trainerName,
        string businessName);

    // KSeF Invoice Events
    /// <summary>
    /// Sends invoice email to the buyer (customer) with PDF attachment
    /// </summary>
    Task SendInvoiceToBuyerEmailAsync(
        string buyerEmail,
        string buyerName,
        string invoiceNumber,
        string sellerName,
        decimal grossAmount,
        string? ksefReferenceNumber,
        DateTime issueDate,
        string description,
        byte[]? invoicePdf);

    /// <summary>
    /// Sends invoice notification email to the seller (business) with PDF attachment
    /// </summary>
    Task SendInvoiceToSellerEmailAsync(
        string sellerEmail,
        string sellerName,
        string invoiceNumber,
        string buyerName,
        decimal grossAmount,
        string? ksefReferenceNumber,
        DateTime issueDate,
        string description,
        byte[]? invoicePdf);

    /// <summary>
    /// Sends invoice notification email to the parent business with child business info
    /// </summary>
    Task SendInvoiceToParentBusinessEmailAsync(
        string parentEmail,
        string parentBusinessName,
        string childBusinessName,
        string invoiceNumber,
        string buyerName,
        decimal grossAmount,
        string? ksefReferenceNumber,
        DateTime issueDate,
        string description,
        byte[]? invoicePdf);

    // Parent-Child Business Association Events

    /// <summary>
    /// Sends email to parent business owner requesting confirmation of child business association.
    /// Links to a frontend page where parent can choose permissions and confirm/reject.
    /// </summary>
    Task SendChildBusinessAssociationRequestEmailAsync(
        string parentEmail,
        string parentBusinessName,
        string childBusinessName,
        string childBusinessEmail,
        string? childBusinessNip,
        string confirmationPageUrl);

    /// <summary>
    /// Sends email to child business when parent confirms the association
    /// </summary>
    Task SendChildBusinessAssociationConfirmedEmailAsync(
        string childEmail,
        string childBusinessName,
        string parentBusinessName,
        bool useParentTPay,
        bool useParentNipForInvoices);

    /// <summary>
    /// Sends email to child business when parent rejects the association
    /// </summary>
    Task SendChildBusinessAssociationRejectedEmailAsync(
        string childEmail,
        string childBusinessName,
        string parentBusinessName,
        string? rejectionReason);

    /// <summary>
    /// Sends email to child business when parent removes the association
    /// </summary>
    Task SendChildBusinessAssociationRemovedEmailAsync(
        string childEmail,
        string childBusinessName,
        string parentBusinessName);

    /// <summary>
    /// Sends email to child business when parent updates permissions
    /// </summary>
    Task SendChildBusinessPermissionsUpdatedEmailAsync(
        string childEmail,
        string childBusinessName,
        string parentBusinessName,
        bool useParentTPay,
        bool useParentNipForInvoices);
}