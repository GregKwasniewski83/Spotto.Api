using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace PlaySpace.Services.Services;

public class EmailService : IEmailService
{
    private readonly EmailConfiguration _config;
    private readonly EmailTemplateService _templateService;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailConfiguration> config, ILogger<EmailService> logger)
    {
        _config = config.Value;
        _templateService = new EmailTemplateService();
        _logger = logger;
    }

    public async Task SendWelcomeEmailAsync(string email, string name)
    {
        var template = _templateService.GetWelcomeTemplate(name);
        await SendEmailAsync(email, name, template.Subject, template.HtmlBody, template.TextBody);
    }

    public async Task SendEmailVerificationAsync(string email, string name, string webUrl, string deepLinkUrl)
    {
        var template = _templateService.GetEmailVerificationTemplate(name, webUrl, deepLinkUrl);
        await SendEmailAsync(email, name, template.Subject, template.HtmlBody, template.TextBody);
    }

    public async Task SendBusinessProfileCreatedEmailAsync(BusinessProfileDto profile)
    {
        if (string.IsNullOrEmpty(profile.Email))
        {
            _logger.LogWarning("Cannot send business profile creation email - no email address provided for profile {ProfileId}", profile.Id);
            return;
        }

        var template = _templateService.GetBusinessProfileCreatedTemplate(profile);
        await SendEmailAsync(profile.Email, profile.DisplayName, template.Subject, template.HtmlBody, template.TextBody);
    }

    public async Task SendTrainerProfileCreatedEmailAsync(TrainerProfileDto profile)
    {
        if (string.IsNullOrEmpty(profile.Email))
        {
            _logger.LogWarning("Cannot send trainer profile creation email - no email address provided for profile {ProfileId}", profile.Id);
            return;
        }

        var template = _templateService.GetTrainerProfileCreatedTemplate(profile);
        await SendEmailAsync(profile.Email, profile.DisplayName, template.Subject, template.HtmlBody, template.TextBody);
    }

    public async Task SendTPayRegistrationSuccessEmailAsync(string email, string name, string merchantId, string activationLink)
    {
        var template = _templateService.GetTPayRegistrationSuccessTemplate(name, merchantId, activationLink);
        await SendEmailAsync(email, name, template.Subject, template.HtmlBody, template.TextBody);
    }

    public async Task SendTPayRegistrationFailureEmailAsync(string email, string name, string errorMessage)
    {
        var template = _templateService.GetTPayRegistrationFailureTemplate(name, errorMessage);
        await SendEmailAsync(email, name, template.Subject, template.HtmlBody, template.TextBody);
    }

    public async Task SendReservationConfirmationEmailAsync(ReservationDto reservation)
    {
        try
        {
            // Extract customer details from the reservation
            string customerEmail = null;
            string customerName = null;
            
            // Try to get customer info from guest details first, then user details
            if (!string.IsNullOrEmpty(reservation.GuestEmail))
            {
                customerEmail = reservation.GuestEmail;
                customerName = reservation.GuestName ?? "Gość";
            }
            
            if (!string.IsNullOrEmpty(customerEmail))
            {
                await SendReservationCreatedEmailAsync(reservation, customerEmail, customerName);
                _logger.LogInformation("Reservation confirmation email sent for reservation {ReservationId}", reservation.Id);
            }
            else
            {
                _logger.LogWarning("Cannot send reservation confirmation email - no customer email found for reservation {ReservationId}", reservation.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reservation confirmation email for reservation {ReservationId}", reservation.Id);
        }
    }

    public async Task SendTrainingReminderEmailAsync(TrainingDto training, string participantEmail)
    {
        // TODO: Implement when TrainingDto is fully available
        _logger.LogInformation("Training reminder email for training {TrainingId} - not implemented yet", training.Id);
        await Task.CompletedTask;
    }

    // New Reservation Event Methods
    public async Task SendReservationCreatedEmailAsync(ReservationDto reservation, string customerEmail, string customerName)
    {
        try
        {
            var timeSlots = string.Join(", ", reservation.TimeSlots);
            var subject = "Rezerwacja potwierdzona!";
            var textBody = $@"Rezerwacja potwierdzona!

Czesc {customerName}!

Twoja rezerwacja zostala pomyslnie potwierdzona!

Szczegoly rezerwacji:
- Obiekt: {reservation.FacilityName ?? "Obiekt sportowy"}
- Data: {reservation.Date:dd.MM.yyyy}
- Godzina: {timeSlots}
- Koszt: {reservation.TotalPrice:N2} zl

Wazne:
- Przybadz 15 minut przed rezerwacja
- Zabierz ze soba wymagane dokumenty i odpowiedni sprzet
- W razie problemow skontaktuj sie z obiektem

Zyczymy udanego treningu!
Zespol Spotto";

            await SendEmailAsync(customerEmail, customerName, subject, null, textBody);
            _logger.LogInformation("Reservation created email sent to {Email} for reservation {ReservationId}", customerEmail, reservation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reservation created email for reservation {ReservationId}", reservation.Id);
        }
    }

    public async Task SendReservationCancelledEmailAsync(ReservationDto reservation, string customerEmail, string customerName)
    {
        try
        {
            var timeSlots = string.Join(", ", reservation.TimeSlots);
            var subject = "Rezerwacja anulowana";
            var textBody = $@"Rezerwacja anulowana

Czesc {customerName}!

Twoja rezerwacja zostala anulowana.

Szczegoly:
- Obiekt: {reservation.FacilityName}
- Data: {reservation.Date:dd.MM.yyyy}
- Godziny: {timeSlots}

Zespol Spotto";

            await SendEmailAsync(customerEmail, customerName, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reservation cancelled email for reservation {ReservationId}", reservation.Id);
        }
    }

    public async Task SendReservationCancelledNoRefundEmailAsync(ReservationDto reservation, string customerEmail, string customerName, BusinessProfileDto business)
    {
        try
        {
            var timeSlots = string.Join(", ", reservation.TimeSlots);
            var contactInfo = new StringBuilder();

            if (!string.IsNullOrEmpty(business.Email))
                contactInfo.AppendLine($"- Email: {business.Email}");
            if (!string.IsNullOrEmpty(business.PhoneNumber))
                contactInfo.AppendLine($"- Telefon: {business.PhoneNumber}");
            if (!string.IsNullOrEmpty(business.Address))
                contactInfo.AppendLine($"- Adres: {business.Address}, {business.PostalCode} {business.City}");

            var subject = "Rezerwacja anulowana - Rozliczenie zgodnie z obowiązującymi zasadami rezerwacji";
            var textBody = $@"Rezerwacja anulowana

Czesc {customerName}!

Twoja rezerwacja zostala anulowana.

Szczegoly rezerwacji:
- Obiekt: {reservation.FacilityName}
- Data: {reservation.Date:dd.MM.yyyy}
- Godziny: {timeSlots}
- Kwota: {reservation.TotalPrice:N2} zl

Rozliczenie anulowanej rezerwacji zgodnie z obowiązującymi zasadami rezerwacji
Automatyczne zwroty sa obecnie wylaczone. Rozliczenie anulowanej rezerwacji zgodnie z obowiązującymi zasadami rezerwacji. Prosimy o bezposredni kontakt z wlascicielem obiektu:

{business.DisplayName}
{contactInfo}

Przepraszamy za niedogodnosci.
Zespol Spotto";

            await SendEmailAsync(customerEmail, customerName, subject, null, textBody);
            _logger.LogInformation("Reservation cancelled (no refund) email sent to {Email} for reservation {ReservationId}", customerEmail, reservation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reservation cancelled (no refund) email for reservation {ReservationId}", reservation.Id);
        }
    }

    public async Task SendReservationCancelledWithRefundEmailAsync(ReservationDto reservation, string customerEmail, string customerName, decimal refundAmount, decimal refundFee)
    {
        try
        {
            var timeSlots = string.Join(", ", reservation.TimeSlots);
            var netRefund = refundAmount - refundFee;

            var subject = "Rezerwacja anulowana - Zwrot potwierdzony";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2E8B57; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .details-box {{ background: white; padding: 15px; margin: 15px 0; border-left: 4px solid #2E8B57; border-radius: 4px; }}
        .refund-box {{ background: #d4edda; border: 1px solid #c3e6cb; color: #155724; padding: 15px; border-radius: 4px; margin: 15px 0; }}
        .fee-note {{ color: #856404; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Rezerwacja Anulowana</h1>
        </div>
        <div class='content'>
            <h2>Czesc {customerName}!</h2>
            <p>Twoja rezerwacja zostala anulowana, a zwrot srodkow zostal zainicjowany.</p>

            <div class='details-box'>
                <h3>Szczegoly rezerwacji:</h3>
                <p><strong>Obiekt:</strong> {reservation.FacilityName}</p>
                <p><strong>Data:</strong> {reservation.Date:dd.MM.yyyy}</p>
                <p><strong>Godziny:</strong> {timeSlots}</p>
                <p><strong>Kwota rezerwacji:</strong> {reservation.TotalPrice:N2} zl</p>
            </div>

            <div class='refund-box'>
                <h3>Szczegoly zwrotu:</h3>
                <p><strong>Kwota do zwrotu:</strong> {netRefund:N2} zl</p>
                {(refundFee > 0 ? $"<p class='fee-note'>Oplata za anulowanie: {refundFee:N2} zl</p>" : "")}
                <p>Zwrot zostanie przetworzony w ciagu <strong>5-10 dni roboczych</strong> na konto, z ktorego dokonano platnosci.</p>
            </div>

            <p>Dziekujemy za korzystanie ze Spotto!</p>
            <p>Sportowe pozdrowienia,<br><strong>Zespol Spotto</strong></p>
        </div>
    </div>
</body>
</html>";

            var textBody = $@"Rezerwacja anulowana - Zwrot potwierdzony

Czesc {customerName}!

Twoja rezerwacja zostala anulowana, a zwrot srodkow zostal zainicjowany.

Szczegoly rezerwacji:
- Obiekt: {reservation.FacilityName}
- Data: {reservation.Date:dd.MM.yyyy}
- Godziny: {timeSlots}
- Kwota rezerwacji: {reservation.TotalPrice:N2} zl

Szczegoly zwrotu:
- Kwota do zwrotu: {netRefund:N2} zl
{(refundFee > 0 ? $"- Oplata za anulowanie: {refundFee:N2} zl" : "")}
- Zwrot zostanie przetworzony w ciagu 5-10 dni roboczych

Dziekujemy za korzystanie ze Spotto!
Zespol Spotto";

            await SendEmailAsync(customerEmail, customerName, subject, htmlBody, textBody);
            _logger.LogInformation("Reservation cancelled with refund email sent to {Email} for reservation {ReservationId}, refund: {RefundAmount}", customerEmail, reservation.Id, netRefund);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reservation cancelled with refund email for reservation {ReservationId}", reservation.Id);
        }
    }

    public async Task SendReservationReminderEmailAsync(ReservationDto reservation, string customerEmail, string customerName)
    {
        try
        {
            var timeSlots = string.Join(", ", reservation.TimeSlots);
            var subject = "Przypomnienie o jutrzejszej rezerwacji!";
            var textBody = $@"Przypomnienie o rezerwacji

Czesc {customerName}!

Jutro masz zaplanowana rezerwacje!

Szczegoly:
- Obiekt: {reservation.FacilityName ?? "Obiekt sportowy"}
- Data: {reservation.Date:dd.MM.yyyy}
- Godzina: {timeSlots}

Pamietaj:
- Sprawdz pogode (dla obiektow zewnetrznych)
- Zabierz odpowiedni sprzet sportowy
- Zaloz wygodna odziez sportowa
- Przybadz 15 minut wczesniej

Do zobaczenia!
Zespol Spotto";

            await SendEmailAsync(customerEmail, customerName, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reservation reminder email for reservation {ReservationId}", reservation.Id);
        }
    }

    public async Task SendNewReservationNotificationEmailAsync(ReservationDto reservation, BusinessProfileDto business)
    {
        if (string.IsNullOrEmpty(business.Email))
        {
            _logger.LogWarning("Cannot send new reservation notification - no email for business {BusinessId}", business.Id);
            return;
        }

        try
        {
            var timeSlots = string.Join(", ", reservation.TimeSlots);
            var subject = "Nowa rezerwacja w Twoim obiekcie!";
            var textBody = $@"Nowa rezerwacja!

Czesc {business.DisplayName}!

Otrzymales nowa rezerwacje!

Szczegoly:
- Obiekt: {reservation.FacilityName ?? "Obiekt sportowy"}
- Data: {reservation.Date:dd.MM.yyyy}
- Godziny: {timeSlots}
- Kwota: {reservation.TotalPrice:N2} zl

Zespol Spotto";

            await SendEmailAsync(business.Email, business.DisplayName, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send new reservation notification to business {BusinessId}", business.Id);
        }
    }

    public async Task SendNewReservationNotificationToParentEmailAsync(ReservationDto reservation, BusinessProfileDto parentBusiness, BusinessProfileDto childBusiness)
    {
        if (string.IsNullOrEmpty(parentBusiness.Email))
        {
            _logger.LogWarning("Cannot send new reservation notification to parent - no email for parent business {ParentBusinessId}", parentBusiness.Id);
            return;
        }

        try
        {
            var timeSlots = string.Join(", ", reservation.TimeSlots);
            var subject = $"Nowa rezerwacja w firmie podrzednej: {childBusiness.DisplayName}";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .info-box {{ background-color: #e3f2fd; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .child-info {{ background-color: #fff3e0; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Nowa Rezerwacja w Firmie Partnerskiej</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{parentBusiness.DisplayName}</strong>,</p>
            <p>Twoja firma partnerska otrzymala nowa rezerwacje!</p>

            <div class='child-info'>
                <h3>Firma partnerska:</h3>
                <p><strong>{childBusiness.DisplayName}</strong></p>
                <p>{childBusiness.City}</p>
            </div>

            <div class='info-box'>
                <h3>Szczegoly rezerwacji:</h3>
                <p><strong>Obiekt:</strong> {reservation.FacilityName ?? "Obiekt sportowy"}</p>
                <p><strong>Data:</strong> {reservation.Date:dd.MM.yyyy}</p>
                <p><strong>Godziny:</strong> {timeSlots}</p>
                <p><strong>Kwota:</strong> {reservation.TotalPrice:N2} zl</p>
            </div>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomosc z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

            var textBody = $@"Nowa rezerwacja w firmie partnerskiej!

Witaj {parentBusiness.DisplayName}!

Twoja firma partnerska otrzymala nowa rezerwacje!

Firma partnerska:
{childBusiness.DisplayName}
{childBusiness.City}

Szczegoly rezerwacji:
- Obiekt: {reservation.FacilityName ?? "Obiekt sportowy"}
- Data: {reservation.Date:dd.MM.yyyy}
- Godziny: {timeSlots}
- Kwota: {reservation.TotalPrice:N2} zl

---
Zespol Spotto";

            await SendEmailAsync(parentBusiness.Email, parentBusiness.DisplayName, subject, htmlBody, textBody);
            _logger.LogInformation("Parent business notification sent to {Email} for reservation in child business {ChildBusinessId}",
                parentBusiness.Email, childBusiness.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send new reservation notification to parent business {ParentBusinessId}", parentBusiness.Id);
        }
    }

    // Training Event Methods
    public async Task SendTrainingBookedEmailAsync(TrainingDto training, string participantEmail, string participantName)
    {
        try
        {
            var trainerName = training.TrainerProfile?.DisplayName ?? "Trener";
            var firstSession = training.Sessions.FirstOrDefault();
            
            if (firstSession != null)
            {
                var template = _templateService.GetTrainingBookedTemplate(
                    participantName,
                    trainerName,
                    firstSession.Date.ToString("dd.MM.yyyy"),
                    $"{firstSession.StartTime} - {firstSession.EndTime}",
                    training.Price
                );
                
                await SendEmailAsync(participantEmail, participantName, template.Subject, template.HtmlBody, template.TextBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send training booked email for training {TrainingId}", training.Id);
        }
    }

    public async Task SendTrainingCancelledEmailAsync(TrainingDto training, string participantEmail, string participantName)
    {
        try
        {
            var trainerName = training.TrainerProfile?.DisplayName ?? "Trener";
            var firstSession = training.Sessions.FirstOrDefault();

            var subject = "Trening anulowany";
            var sessionInfo = firstSession != null
                ? $"- Data: {firstSession.Date:dd.MM.yyyy}\n- Godzina: {firstSession.StartTime} - {firstSession.EndTime}"
                : "";
            var textBody = $@"Trening anulowany

Czesc {participantName}!

Niestety, Twoj trening zostal anulowany przez trenera.

Szczegoly:
- Trening: {training.Title}
- Trener: {trainerName}
{sessionInfo}

Skontaktujemy sie z Toba w sprawie zwrotu srodkow.

Zespol Spotto";

            await SendEmailAsync(participantEmail, participantName, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send training cancelled email for training {TrainingId}", training.Id);
        }
    }

    public async Task SendTrainingReminderEmailAsync(TrainingDto training, string participantEmail, string participantName)
    {
        try
        {
            var trainerName = training.TrainerProfile?.DisplayName ?? "Trener";
            var firstSession = training.Sessions.FirstOrDefault();

            var subject = "Przypomnienie o treningu za 2 godziny!";
            var sessionInfo = firstSession != null
                ? $"- Data: {firstSession.Date:dd.MM.yyyy}\n- Godzina: {firstSession.StartTime} - {firstSession.EndTime}"
                : "";
            var textBody = $@"Przypomnienie o treningu!

Czesc {participantName}!

Przypominamy o Twoim treningu za 2 godziny!

Szczegoly:
- Trening: {training.Title}
- Trener: {trainerName}
{sessionInfo}

Powodzenia!

Zespol Spotto";

            await SendEmailAsync(participantEmail, participantName, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send training reminder email for training {TrainingId}", training.Id);
        }
    }

    public async Task SendNewTrainingBookingEmailAsync(TrainingDto training, TrainerProfileDto trainer)
    {
        if (string.IsNullOrEmpty(trainer.Email))
        {
            _logger.LogWarning("Cannot send new training booking notification - no email for trainer {TrainerId}", trainer.Id);
            return;
        }

        try
        {
            var firstSession = training.Sessions.FirstOrDefault();

            var subject = "Nowe zgloszenie na trening!";
            var sessionInfo = firstSession != null
                ? $"- Data: {firstSession.Date:dd.MM.yyyy}\n- Godzina: {firstSession.StartTime} - {firstSession.EndTime}"
                : "";
            var textBody = $@"Nowe zgloszenie na trening!

Czesc {trainer.DisplayName}!

Masz nowe zgloszenie na trening!

Szczegoly:
- Trening: {training.Title}
- Opis: {training.Description ?? "Brak opisu"}
{sessionInfo}
- Cena: {training.Price:N2} zl
- Max uczestnikow: {training.MaxParticipants}

Zespol Spotto";

            await SendEmailAsync(trainer.Email, trainer.DisplayName, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send new training booking notification to trainer {TrainerId}", trainer.Id);
        }
    }

    // Payment Event Methods
    public async Task SendPaymentSuccessfulEmailAsync(PaymentDto payment, string customerEmail, string customerName)
    {
        try
        {
            var template = _templateService.GetPaymentSuccessfulTemplate(
                customerName,
                payment.Description ?? "Płatność PlaySpace",
                payment.Amount,
                payment.TPayTransactionId ?? payment.Id.ToString()
            );
            
            await SendEmailAsync(customerEmail, customerName, template.Subject, template.HtmlBody, template.TextBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment successful email for payment {PaymentId}", payment.Id);
        }
    }

    public async Task SendPaymentFailedEmailAsync(string customerEmail, string customerName, string errorMessage, decimal amount)
    {
        try
        {
            var subject = "Problem z platnoscia";
            var textBody = $@"Problem z platnoscia

Czesc {customerName}!

Niestety, wystapil problem z Twoja platnoscia.

Szczegoly:
- Kwota: {amount:N2} zl
- Blad: {errorMessage}

Sprobuj ponownie lub skontaktuj sie z nami.

Zespol Spotto";

            await SendEmailAsync(customerEmail, customerName, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment failed email to {Email}", customerEmail);
        }
    }

    public async Task SendPaymentReceivedEmailAsync(PaymentDto payment, string providerEmail, string providerName)
    {
        try
        {
            var subject = "Otrzymano platnosc od klienta!";
            var textBody = $@"Otrzymano platnosc!

Czesc {providerName}!

Otrzymales platnosc od klienta!

Szczegoly:
- Kwota: {payment.Amount:N2} zl
- Opis: {payment.Description}

Srodki zostana przelane na Twoje konto zgodnie z harmonogramem wyplat.

Zespol Spotto";

            await SendEmailAsync(providerEmail, providerName, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment received email for payment {PaymentId}", payment.Id);
        }
    }

    // Account Event Methods
    public async Task SendPasswordChangedEmailAsync(string email, string name)
    {
        try
        {
            var subject = "Haslo zostalo zmienione";
            var textBody = $@"Haslo zmienione

Czesc {name}!

Twoje haslo do konta Spotto zostalo zmienione.

Jesli to nie Ty, natychmiast skontaktuj sie z nami!

Zespol Spotto";

            await SendEmailAsync(email, name, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password changed email to {Email}", email);
        }
    }

    public async Task SendEmailChangedNotificationEmailAsync(string oldEmail, string newEmail, string name)
    {
        try
        {
            var subject = "Adres email zostal zmieniony";
            var textBody = $@"Adres email zmieniony

Czesc {name}!

Twoj adres email zostal zmieniony z {oldEmail} na {newEmail}.

Jesli to nie Ty, natychmiast skontaktuj sie z nami!

Zespol Spotto";

            // Send to both old and new email
            await SendEmailAsync(oldEmail, name, subject, null, textBody);
            await SendEmailAsync(newEmail, name, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email changed notification");
        }
    }

    public async Task SendAccountDeletedEmailAsync(string email, string name, string? reason)
    {
        try
        {
            var subject = "Konto usuniete - PlaySpace";
            var textBody = $@"Konto usuniete

Czesc {name}!

Twoje konto PlaySpace zostalo pomyslnie usuniete.
{(!string.IsNullOrEmpty(reason) ? $"\nPowod: {reason}" : "")}

Wszystkie Twoje dane osobowe zostaly trwale usuniete z naszego systemu.

Jesli to byl blad lub chcesz utworzyc nowe konto, mozesz zarejestrowac sie ponownie w dowolnym momencie.

Dziekujemy za korzystanie z PlaySpace.

Zespol Spotto";

            await SendEmailAsync(email, name, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send account deleted email to {Email}", email);
        }
    }

    // TPay Event Methods
    public async Task SendTPayVerificationCompleteEmailAsync(string email, string name, string verificationStatus)
    {
        try
        {
            var subject = verificationStatus == "verified" ? "TPay - Weryfikacja zakonczona!" : "TPay - Problem z weryfikacja";
            var statusMessage = verificationStatus == "verified"
                ? "Gratulacje! Mozesz teraz otrzymywac platnosci."
                : "Skontaktuj sie z TPay w sprawie dalszych krokow.";
            var textBody = $@"TPay - Status weryfikacji

Czesc {name}!

Status weryfikacji TPay: {verificationStatus}

{statusMessage}

Zespol Spotto";

            await SendEmailAsync(email, name, subject, null, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send TPay verification email to {Email}", email);
        }
    }

    public async Task SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl)
    {
        var subject = "Spotto - Reset hasla";
        var textBody = $@"Reset hasla - PlaySpace

Otrzymalismy zadanie resetowania hasla dla Twojego konta PlaySpace.

Aby zresetowac haslo, przejdz do: {resetUrl}?token={resetToken}

UWAGA:
- Link jest wazny przez 24 godziny
- Jesli nie zadales resetu hasla, zignoruj ten email
- Ze wzgledow bezpieczenstwa nie udostepniaj tego linku

Pozdrawiamy,
Zespol Spotto";

        await SendEmailAsync(email, null, subject, null, textBody);
    }

    public async Task SendNotificationEmailAsync(string email, string subject, string body, bool isHtml = true)
    {
        await SendEmailAsync(email, null, subject, isHtml ? body : null, isHtml ? null : body);
    }

    public async Task SendAgentInvitationEmailAsync(string email, string subject, string htmlBody)
    {
        await SendEmailAsync(email, null, subject, htmlBody, null);
    }

    // KSeF Invoice Email Methods
    public async Task SendInvoiceToBuyerEmailAsync(
        string buyerEmail,
        string buyerName,
        string invoiceNumber,
        string sellerName,
        decimal grossAmount,
        string? ksefReferenceNumber,
        DateTime issueDate,
        string description,
        byte[]? invoicePdf)
    {
        try
        {
            var subject = $"Faktura {invoiceNumber} od {sellerName}";
            var textBody = $@"Faktura {invoiceNumber}

Czesc {buyerName}!

Wystawilismy dla Ciebie fakture za zakupione uslugi.

Numer faktury: {invoiceNumber}
Data wystawienia: {issueDate:dd.MM.yyyy}
Sprzedawca: {sellerName}
Opis: {description}
Kwota brutto: {grossAmount:N2} zl
{(!string.IsNullOrEmpty(ksefReferenceNumber) ? $"Numer KSeF: {ksefReferenceNumber}" : "")}

Faktura w formacie PDF znajduje sie w zalaczniku.

Faktura zostala wystawiona w Krajowym Systemie e-Faktur (KSeF).

Dziekujemy za skorzystanie z naszych uslug!
Zespol Spotto";

            await SendEmailWithAttachmentAsync(
                buyerEmail,
                buyerName,
                subject,
                textBody,
                invoicePdf,
                $"Faktura_{invoiceNumber.Replace("/", "-")}.pdf");

            _logger.LogInformation("[KSeF] Invoice email sent to buyer {Email} for invoice {InvoiceNumber}", buyerEmail, invoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KSeF] Failed to send invoice email to buyer {Email} for invoice {InvoiceNumber}", buyerEmail, invoiceNumber);
        }
    }

    public async Task SendInvoiceToSellerEmailAsync(
        string sellerEmail,
        string sellerName,
        string invoiceNumber,
        string buyerName,
        decimal grossAmount,
        string? ksefReferenceNumber,
        DateTime issueDate,
        string description,
        byte[]? invoicePdf)
    {
        try
        {
            var subject = $"Wystawiono fakture {invoiceNumber} dla {buyerName}";
            var textBody = $@"Faktura {invoiceNumber}

Czesc {sellerName}!

Wystawiono nowa fakture w Twoim imieniu.

Numer faktury: {invoiceNumber}
Data wystawienia: {issueDate:dd.MM.yyyy}
Nabywca: {buyerName}
Opis: {description}
Kwota brutto: {grossAmount:N2} zl
{(!string.IsNullOrEmpty(ksefReferenceNumber) ? $"Zarejestrowano w KSeF: {ksefReferenceNumber}" : "")}

Faktura w formacie PDF znajduje sie w zalaczniku.

Faktura zostala automatycznie wystawiona przez system Spotto i przeslana do KSeF.

Zespol Spotto";

            await SendEmailWithAttachmentAsync(
                sellerEmail,
                sellerName,
                subject,
                textBody,
                invoicePdf,
                $"Faktura_{invoiceNumber.Replace("/", "-")}.pdf");

            _logger.LogInformation("[KSeF] Invoice email sent to seller {Email} for invoice {InvoiceNumber}", sellerEmail, invoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KSeF] Failed to send invoice email to seller {Email} for invoice {InvoiceNumber}", sellerEmail, invoiceNumber);
        }
    }

    public async Task SendInvoiceToParentBusinessEmailAsync(
        string parentEmail,
        string parentBusinessName,
        string childBusinessName,
        string invoiceNumber,
        string buyerName,
        decimal grossAmount,
        string? ksefReferenceNumber,
        DateTime issueDate,
        string description,
        byte[]? invoicePdf)
    {
        try
        {
            var subject = $"Faktura {invoiceNumber} wystawiona przez firme partnerska: {childBusinessName}";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .info-box {{ background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .child-info {{ background-color: #fff3e0; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Nowa Faktura od Firmy Partnerskiej</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{parentBusinessName}</strong>,</p>
            <p>Twoja firma partnerska wystawila nowa fakture.</p>

            <div class='child-info'>
                <h3>Firma partnerska:</h3>
                <p><strong>{childBusinessName}</strong></p>
            </div>

            <div class='info-box'>
                <h3>Szczegoly faktury:</h3>
                <p><strong>Numer faktury:</strong> {invoiceNumber}</p>
                <p><strong>Data wystawienia:</strong> {issueDate:dd.MM.yyyy}</p>
                <p><strong>Nabywca:</strong> {buyerName}</p>
                <p><strong>Opis:</strong> {description}</p>
                <p><strong>Kwota brutto:</strong> {grossAmount:N2} zl</p>
                {(!string.IsNullOrEmpty(ksefReferenceNumber) ? $"<p><strong>Numer KSeF:</strong> {ksefReferenceNumber}</p>" : "")}
            </div>

            <p>Faktura w formacie PDF znajduje sie w zalaczniku.</p>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomosc z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

            var textBody = $@"Nowa faktura od firmy partnerskiej!

Witaj {parentBusinessName}!

Twoja firma partnerska wystawila nowa fakture.

Firma partnerska: {childBusinessName}

Szczegoly faktury:
- Numer faktury: {invoiceNumber}
- Data wystawienia: {issueDate:dd.MM.yyyy}
- Nabywca: {buyerName}
- Opis: {description}
- Kwota brutto: {grossAmount:N2} zl
{(!string.IsNullOrEmpty(ksefReferenceNumber) ? $"- Numer KSeF: {ksefReferenceNumber}" : "")}

Faktura w formacie PDF znajduje sie w zalaczniku.

---
Zespol Spotto";

            await SendEmailWithAttachmentAsync(
                parentEmail,
                parentBusinessName,
                subject,
                textBody,
                invoicePdf,
                $"Faktura_{invoiceNumber.Replace("/", "-")}.pdf");

            _logger.LogInformation("[KSeF] Invoice email sent to parent business {Email} for invoice {InvoiceNumber} from child {ChildBusiness}",
                parentEmail, invoiceNumber, childBusinessName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KSeF] Failed to send invoice email to parent business {Email} for invoice {InvoiceNumber}",
                parentEmail, invoiceNumber);
        }
    }

    private async Task SendEmailWithAttachmentAsync(
        string toEmail,
        string? toName,
        string subject,
        string textBody,
        byte[]? attachmentData,
        string? attachmentFileName)
    {
        try
        {
            if (!IsConfigured())
            {
                _logger.LogWarning("Email service is not configured. Email to {Email} with subject '{Subject}' will not be sent.", toEmail, subject);
                return;
            }

            _logger.LogInformation("Sending email with attachment to {Email} with subject '{Subject}'", toEmail, subject);

            using var client = new SmtpClient(_config.SmtpServer, _config.SmtpPort);
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_config.Username, _config.Password);
            client.EnableSsl = _config.EnableSsl;

            using var message = new MailMessage();
            message.From = new MailAddress(_config.FromEmail, _config.FromName);
            message.To.Add(new MailAddress(toEmail, toName ?? toEmail));
            message.Subject = subject;
            message.SubjectEncoding = Encoding.UTF8;
            message.BodyEncoding = Encoding.UTF8;
            message.Body = textBody;
            message.IsBodyHtml = false;

            // Add PDF attachment if provided
            if (attachmentData != null && attachmentData.Length > 0 && !string.IsNullOrEmpty(attachmentFileName))
            {
                var stream = new MemoryStream(attachmentData);
                var attachment = new Attachment(stream, attachmentFileName, "application/pdf");
                message.Attachments.Add(attachment);
                _logger.LogDebug("Added PDF attachment: {FileName}, size: {Size} bytes", attachmentFileName, attachmentData.Length);
            }

            await client.SendMailAsync(message);

            _logger.LogInformation("Email with attachment sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email with attachment to {Email} with subject '{Subject}': {Error}", toEmail, subject, ex.Message);
        }
    }

    private async Task SendEmailAsync(string toEmail, string? toName, string subject, string? htmlBody, string? textBody)
    {
        try
        {
            if (!IsConfigured())
            {
                _logger.LogWarning("Email service is not configured. Email to {Email} with subject '{Subject}' will not be sent.", toEmail, subject);
                return;
            }

            _logger.LogInformation("Sending email to {Email} with subject '{Subject}'", toEmail, subject);

            using var client = new SmtpClient(_config.SmtpServer, _config.SmtpPort);
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_config.Username, _config.Password);
            client.EnableSsl = _config.EnableSsl;

            using var message = new MailMessage();
            message.From = new MailAddress(_config.FromEmail, _config.FromName);
            message.To.Add(new MailAddress(toEmail, toName ?? toEmail));
            message.Subject = subject;
            message.SubjectEncoding = Encoding.UTF8;
            message.BodyEncoding = Encoding.UTF8;

            // Add both HTML and plain text versions using AlternateViews for proper multipart/alternative
            if (!string.IsNullOrEmpty(htmlBody) && !string.IsNullOrEmpty(textBody))
            {
                // Use AlternateViews for proper multipart/alternative email
                // Plain text first (lower priority), HTML second (higher priority)
                var plainTextView = AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, "text/plain");
                var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");

                message.AlternateViews.Add(plainTextView);
                message.AlternateViews.Add(htmlView);
            }
            else if (!string.IsNullOrEmpty(htmlBody))
            {
                message.Body = htmlBody;
                message.IsBodyHtml = true;
            }
            else if (!string.IsNullOrEmpty(textBody))
            {
                message.Body = textBody;
                message.IsBodyHtml = false;
            }
            else
            {
                throw new ArgumentException("Either htmlBody or textBody must be provided");
            }

            await client.SendMailAsync(message);
            
            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} with subject '{Subject}': {Error}", toEmail, subject, ex.Message);
            
            // Don't rethrow - email failures shouldn't break the main application flow
            // but log the error for monitoring
        }
    }

    public async Task SendPartialCancellationWithRefundEmailAsync(
        Guid reservationId,
        string facilityName,
        DateTime reservationDate,
        List<string> cancelledSlots,
        List<string> remainingSlots,
        decimal cancelledAmount,
        decimal refundAmount,
        decimal refundFee,
        string customerEmail,
        string customerName)
    {
        var subject = $"Potwierdzenie czesciowego anulowania - Rezerwacja {reservationId.ToString().Substring(0, 8)}";

        var remainingSlotsText = remainingSlots.Any()
            ? $"Pozostale aktywne godziny:\n{string.Join("\n", remainingSlots.Select(s => $"- {s}"))}"
            : "Uwaga: Wszystkie godziny zostaly anulowane. Twoja rezerwacja jest teraz calkowicie anulowana.";

        var textBody = $@"Czesciowe anulowanie potwierdzone

Witaj {customerName},

Pomyslnie przetworzylismy Twoje zadanie czesciowego anulowania rezerwacji.

Szczegoly rezerwacji:
- Obiekt: {facilityName}
- Data: {reservationDate:dd.MM.yyyy}

Anulowane godziny:
{string.Join("\n", cancelledSlots.Select(s => $"- {s}"))}

{remainingSlotsText}

Szczegoly zwrotu:
- Kwota anulowana: {cancelledAmount:N2} zl
- Oplata za zwrot: -{refundFee:N2} zl
- Kwota zwrotu netto: {refundAmount:N2} zl

Zwrot zostanie przetworzony w ciagu 5-10 dni roboczych i pojawi sie na Twoim oryginalnym sposobie platnosci.

W razie pytan prosimy o kontakt.

---
To jest automatyczna wiadomosc z PlaySpace. Prosimy nie odpowiadac.";

        await SendEmailAsync(customerEmail, customerName, subject, null, textBody);
    }

    public async Task SendPartialCancellationNoRefundEmailAsync(
        Guid reservationId,
        string facilityName,
        DateTime reservationDate,
        List<string> cancelledSlots,
        List<string> remainingSlots,
        decimal cancelledAmount,
        string customerEmail,
        string customerName,
        BusinessProfileDto business,
        string reason)
    {
        var subject = $"Potwierdzenie czesciowego anulowania - Rezerwacja {reservationId.ToString().Substring(0, 8)}";

        var remainingSlotsText = remainingSlots.Any()
            ? $"Pozostale aktywne godziny:\n{string.Join("\n", remainingSlots.Select(s => $"- {s}"))}"
            : "Uwaga: Wszystkie godziny zostaly anulowane. Twoja rezerwacja jest teraz calkowicie anulowana.";

        var contactInfo = new StringBuilder();
        contactInfo.AppendLine(business.DisplayName ?? business.CompanyName);
        if (!string.IsNullOrEmpty(business.Email))
            contactInfo.AppendLine($"Email: {business.Email}");
        if (!string.IsNullOrEmpty(business.PhoneNumber))
            contactInfo.AppendLine($"Telefon: {business.PhoneNumber}");
        if (!string.IsNullOrEmpty(business.Address))
            contactInfo.AppendLine($"Adres: {business.Address}, {business.City}, {business.PostalCode}");

        var textBody = $@"Czesciowe anulowanie potwierdzone

Witaj {customerName},

Pomyslnie przetworzylismy Twoje zadanie czesciowego anulowania rezerwacji.

Szczegoly rezerwacji:
- Obiekt: {facilityName}
- Data: {reservationDate:dd.MM.yyyy}

Anulowane godziny:
{string.Join("\n", cancelledSlots.Select(s => $"- {s}"))}

{remainingSlotsText}

ZWROT ZOSTANIE OBSLUZONY PRZEZ OBIEKT
- Kwota anulowana: {cancelledAmount:N2} zl
- Powod: {reason}

Twoje godziny zostaly anulowane. Zwrot srodkow zostanie przetworzony bezposrednio przez wlasciciela obiektu.

Obiekt skontaktuje sie z Toba w celu ustalenia szczegolow zwrotu lub mozesz skontaktowac sie z nimi bezposrednio.

KONTAKT Z OBIEKTEM:
{contactInfo}
---
To jest automatyczna wiadomosc z PlaySpace. Prosimy nie odpowiadac.";

        await SendEmailAsync(customerEmail, customerName, subject, null, textBody);
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_config.SmtpServer) &&
               !string.IsNullOrEmpty(_config.Username) &&
               !string.IsNullOrEmpty(_config.Password) &&
               !string.IsNullOrEmpty(_config.FromEmail);
    }

    public async Task SendTrainerAssociationRequestEmailAsync(
        string businessEmail,
        string businessName,
        string trainerName,
        string trainerEmail,
        string confirmationPageUrl)
    {
        var subject = $"Prośba o powiązanie konta trenera - {trainerName}";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .btn {{ display: inline-block; padding: 12px 24px; margin: 10px 5px; text-decoration: none; border-radius: 5px; font-weight: bold; }}
        .btn-primary {{ background-color: #4CAF50; color: white; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Prośba o Powiązanie Konta Trenera</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{businessName}</strong>,</p>
            <p>Trener <strong>{trainerName}</strong> ({trainerEmail}) chce powiązać swoje konto z Twoją firmą w aplikacji Spotto.</p>
            <p>Możesz zaakceptować tę prośbę i określić uprawnienia trenera:</p>
            <ul>
                <li><strong>Własne treningi</strong> - trener może prowadzić własne treningi w Twojej lokalizacji</li>
                <li><strong>Pracownik</strong> - trener pracuje jako pracownik Twojej firmy</li>
            </ul>
            <p style='text-align: center; margin-top: 30px;'>
                <a href='{confirmationPageUrl}' class='btn btn-primary'>Rozpatrz Prośbę</a>
            </p>
            <p style='margin-top: 20px; font-size: 12px; color: #666;'>
                Link jest ważny przez 7 dni. Na stronie będziesz mógł zaakceptować lub odrzucić prośbę.
            </p>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto. Prosimy nie odpowiadać na tego maila.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Prośba o Powiązanie Konta Trenera

Witaj {businessName},

Trener {trainerName} ({trainerEmail}) chce powiązać swoje konto z Twoją firmą w aplikacji Spotto.

Możesz zaakceptować tę prośbę i określić uprawnienia trenera:
- Własne treningi - trener może prowadzić własne treningi w Twojej lokalizacji
- Pracownik - trener pracuje jako pracownik Twojej firmy

Aby rozpatrzyć prośbę, kliknij: {confirmationPageUrl}

Link jest ważny przez 7 dni.

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(businessEmail, businessName, subject, htmlBody, textBody);
    }

    public async Task SendTrainerAssociationConfirmedEmailAsync(
        string trainerEmail,
        string trainerName,
        string businessName)
    {
        var subject = $"Powiązanie z firmą {businessName} zostało zaakceptowane";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Powiązanie Zatwierdzone!</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{trainerName}</strong>,</p>
            <p>Świetna wiadomość! Firma <strong>{businessName}</strong> zaakceptowała Twoją prośbę o powiązanie.</p>
            <p>Od teraz:</p>
            <ul>
                <li>Firma będzie widoczna w sekcji 'Powiązane firmy' na Twoim profilu</li>
                <li>Klienci będą widzieć to powiązanie podczas przeglądania Twojego profilu</li>
            </ul>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Powiązanie Zatwierdzone!

Witaj {trainerName},

Świetna wiadomość! Firma {businessName} zaakceptowała Twoją prośbę o powiązanie.

Od teraz firma będzie widoczna w sekcji 'Powiązane firmy' na Twoim profilu.

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(trainerEmail, trainerName, subject, htmlBody, textBody);
    }

    public async Task SendTrainerAssociationRejectedEmailAsync(
        string trainerEmail,
        string trainerName,
        string businessName,
        string? rejectionReason)
    {
        var subject = $"Powiązanie z firmą {businessName} zostało odrzucone";

        var reasonText = !string.IsNullOrEmpty(rejectionReason)
            ? $"<p><strong>Powód:</strong> {rejectionReason}</p>"
            : "";

        var reasonTextPlain = !string.IsNullOrEmpty(rejectionReason)
            ? $"\nPowód: {rejectionReason}"
            : "";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f44336; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Powiązanie Odrzucone</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{trainerName}</strong>,</p>
            <p>Niestety firma <strong>{businessName}</strong> odrzuciła Twoją prośbę o powiązanie.</p>
            {reasonText}
            <p>Możesz spróbować skontaktować się z firmą bezpośrednio lub poszukać innych partnerów biznesowych.</p>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Powiązanie Odrzucone

Witaj {trainerName},

Niestety firma {businessName} odrzuciła Twoją prośbę o powiązanie.{reasonTextPlain}

Możesz spróbować skontaktować się z firmą bezpośrednio lub poszukać innych partnerów biznesowych.

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(trainerEmail, trainerName, subject, htmlBody, textBody);
    }

    public async Task SendAssociationRemovedEmailAsync(
        string trainerEmail,
        string trainerName,
        string businessName)
    {
        var subject = $"Powiązanie z firmą {businessName} zostało usunięte";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #ff9800; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Powiązanie Usunięte</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{trainerName}</strong>,</p>
            <p>Firma <strong>{businessName}</strong> usunęła powiązanie z Twoim profilem trenera.</p>
            <p>Twoje przyszłe treningi zaplanowane w tej firmie mogą wymagać ponownego uzgodnienia.</p>
            <p>Jeśli masz pytania, skontaktuj się bezpośrednio z przedstawicielem firmy.</p>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Powiązanie Usunięte

Witaj {trainerName},

Firma {businessName} usunęła powiązanie z Twoim profilem trenera.

Twoje przyszłe treningi zaplanowane w tej firmie mogą wymagać ponownego uzgodnienia.

Jeśli masz pytania, skontaktuj się bezpośrednio z przedstawicielem firmy.

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(trainerEmail, trainerName, subject, htmlBody, textBody);
    }

    // Parent-Child Business Association Emails

    public async Task SendChildBusinessAssociationRequestEmailAsync(
        string parentEmail,
        string parentBusinessName,
        string childBusinessName,
        string childBusinessEmail,
        string? childBusinessNip,
        string confirmationPageUrl)
    {
        var subject = $"Prośba o powiązanie jako firma partnerska - {childBusinessName}";

        var nipInfo = !string.IsNullOrEmpty(childBusinessNip)
            ? $"<p><strong>NIP:</strong> {childBusinessNip}</p>"
            : "";

        var nipInfoPlain = !string.IsNullOrEmpty(childBusinessNip)
            ? $"\nNIP: {childBusinessNip}"
            : "";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .btn {{ display: inline-block; padding: 12px 24px; margin: 10px 5px; text-decoration: none; border-radius: 5px; font-weight: bold; }}
        .btn-primary {{ background-color: #2196F3; color: white; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .info-box {{ background-color: #e3f2fd; padding: 15px; border-radius: 5px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Prośba o Powiązanie Firmy Partnerskiej</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{parentBusinessName}</strong>,</p>
            <p>Firma <strong>{childBusinessName}</strong> chce działać jako firma partnerska pod Twoją firmą w aplikacji Spotto.</p>

            <div class='info-box'>
                <p><strong>Dane firmy wnioskującej:</strong></p>
                <p><strong>Nazwa:</strong> {childBusinessName}</p>
                <p><strong>Email:</strong> {childBusinessEmail}</p>
                {nipInfo}
            </div>

            <p>Jako firma nadrzędna możesz przyznać następujące uprawnienia:</p>
            <ul>
                <li><strong>Użycie TPay</strong> - firma partnerska będzie mogła korzystać z Twojej integracji płatności TPay</li>
                <li><strong>Użycie NIP do faktur</strong> - firma partnerska będzie wystawiać faktury na Twoje dane firmowe (NIP)</li>
            </ul>
            <p style='text-align: center; margin-top: 30px;'>
                <a href='{confirmationPageUrl}' class='btn btn-primary'>Rozpatrz Prośbę</a>
            </p>
            <p style='margin-top: 20px; font-size: 12px; color: #666;'>
                Link jest ważny przez 7 dni. Na stronie będziesz mógł zaakceptować lub odrzucić prośbę oraz określić uprawnienia.
            </p>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto. Prosimy nie odpowiadać na tego maila.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Prośba o Powiązanie Firmy Partnerskiej

Witaj {parentBusinessName},

Firma {childBusinessName} chce działać jako firma partnerska pod Twoją firmą w aplikacji Spotto.

Dane firmy wnioskującej:
Nazwa: {childBusinessName}
Email: {childBusinessEmail}{nipInfoPlain}

Jako firma nadrzędna możesz przyznać następujące uprawnienia:
- Użycie TPay - firma partnerska będzie mogła korzystać z Twojej integracji płatności TPay
- Użycie NIP do faktur - firma partnerska będzie wystawiać faktury na Twoje dane firmowe (NIP)

Aby rozpatrzyć prośbę, kliknij: {confirmationPageUrl}

Link jest ważny przez 7 dni.

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(parentEmail, parentBusinessName, subject, htmlBody, textBody);
    }

    public async Task SendChildBusinessAssociationConfirmedEmailAsync(
        string childEmail,
        string childBusinessName,
        string parentBusinessName,
        bool useParentTPay,
        bool useParentNipForInvoices)
    {
        var subject = $"Powiązanie z firmą {parentBusinessName} zostało zaakceptowane";

        var permissions = new List<string>();
        if (useParentTPay) permissions.Add("Możesz korzystać z integracji płatności TPay firmy nadrzędnej");
        if (useParentNipForInvoices) permissions.Add("Faktury będą wystawiane na dane firmy nadrzędnej (NIP)");

        var permissionsHtml = permissions.Any()
            ? $"<p><strong>Przyznane uprawnienia:</strong></p><ul>{string.Join("", permissions.Select(p => $"<li>{p}</li>"))}</ul>"
            : "<p>Powiązanie zostało utworzone bez dodatkowych uprawnień.</p>";

        var permissionsPlain = permissions.Any()
            ? $"\nPrzyznane uprawnienia:\n{string.Join("\n", permissions.Select(p => $"- {p}"))}"
            : "\nPowiązanie zostało utworzone bez dodatkowych uprawnień.";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Powiązanie Zatwierdzone!</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{childBusinessName}</strong>,</p>
            <p>Świetna wiadomość! Firma <strong>{parentBusinessName}</strong> zaakceptowała Twoją prośbę o powiązanie jako firma partnerska.</p>
            {permissionsHtml}
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Powiązanie Zatwierdzone!

Witaj {childBusinessName},

Świetna wiadomość! Firma {parentBusinessName} zaakceptowała Twoją prośbę o powiązanie jako firma partnerska.
{permissionsPlain}

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(childEmail, childBusinessName, subject, htmlBody, textBody);
    }

    public async Task SendChildBusinessAssociationRejectedEmailAsync(
        string childEmail,
        string childBusinessName,
        string parentBusinessName,
        string? rejectionReason)
    {
        var subject = $"Powiązanie z firmą {parentBusinessName} zostało odrzucone";

        var reasonHtml = !string.IsNullOrEmpty(rejectionReason)
            ? $"<p><strong>Powód:</strong> {rejectionReason}</p>"
            : "";

        var reasonPlain = !string.IsNullOrEmpty(rejectionReason)
            ? $"\nPowód: {rejectionReason}"
            : "";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f44336; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Powiązanie Odrzucone</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{childBusinessName}</strong>,</p>
            <p>Niestety firma <strong>{parentBusinessName}</strong> odrzuciła Twoją prośbę o powiązanie jako firma partnerska.</p>
            {reasonHtml}
            <p>Możesz spróbować skontaktować się z firmą bezpośrednio lub poszukać innych partnerów biznesowych.</p>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Powiązanie Odrzucone

Witaj {childBusinessName},

Niestety firma {parentBusinessName} odrzuciła Twoją prośbę o powiązanie jako firma partnerska.{reasonPlain}

Możesz spróbować skontaktować się z firmą bezpośrednio lub poszukać innych partnerów biznesowych.

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(childEmail, childBusinessName, subject, htmlBody, textBody);
    }

    public async Task SendChildBusinessAssociationRemovedEmailAsync(
        string childEmail,
        string childBusinessName,
        string parentBusinessName)
    {
        var subject = $"Powiązanie z firmą {parentBusinessName} zostało usunięte";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #ff9800; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Powiązanie Usunięte</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{childBusinessName}</strong>,</p>
            <p>Firma <strong>{parentBusinessName}</strong> usunęła powiązanie z Twoją firmą.</p>
            <p>Od teraz:</p>
            <ul>
                <li>Nie możesz już korzystać z integracji TPay firmy nadrzędnej</li>
                <li>Faktury nie będą już wystawiane na dane firmy nadrzędnej</li>
            </ul>
            <p>Jeśli masz pytania, skontaktuj się bezpośrednio z przedstawicielem firmy.</p>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Powiązanie Usunięte

Witaj {childBusinessName},

Firma {parentBusinessName} usunęła powiązanie z Twoją firmą.

Od teraz:
- Nie możesz już korzystać z integracji TPay firmy nadrzędnej
- Faktury nie będą już wystawiane na dane firmy nadrzędnej

Jeśli masz pytania, skontaktuj się bezpośrednio z przedstawicielem firmy.

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(childEmail, childBusinessName, subject, htmlBody, textBody);
    }

    public async Task SendChildBusinessPermissionsUpdatedEmailAsync(
        string childEmail,
        string childBusinessName,
        string parentBusinessName,
        bool useParentTPay,
        bool useParentNipForInvoices)
    {
        var subject = $"Zaktualizowano uprawnienia powiązania z firmą {parentBusinessName}";

        var permissionsList = new List<string>();
        permissionsList.Add(useParentTPay
            ? "✓ Możesz korzystać z integracji płatności TPay firmy nadrzędnej"
            : "✗ Nie możesz korzystać z integracji płatności TPay firmy nadrzędnej");
        permissionsList.Add(useParentNipForInvoices
            ? "✓ Faktury będą wystawiane na dane firmy nadrzędnej (NIP)"
            : "✗ Faktury będą wystawiane na Twoje własne dane firmowe");

        var permissionsHtml = string.Join("", permissionsList.Select(p => $"<li>{p}</li>"));
        var permissionsPlain = string.Join("\n", permissionsList.Select(p => $"  {p}"));

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Uprawnienia Zaktualizowane</h1>
        </div>
        <div class='content'>
            <p>Witaj <strong>{childBusinessName}</strong>,</p>
            <p>Firma <strong>{parentBusinessName}</strong> zaktualizowała uprawnienia Twojego powiązania.</p>
            <p><strong>Aktualne uprawnienia:</strong></p>
            <ul>{permissionsHtml}</ul>
        </div>
        <div class='footer'>
            <p>To jest automatyczna wiadomość z aplikacji Spotto.</p>
        </div>
    </div>
</body>
</html>";

        var textBody = $@"Uprawnienia Zaktualizowane

Witaj {childBusinessName},

Firma {parentBusinessName} zaktualizowała uprawnienia Twojego powiązania.

Aktualne uprawnienia:
{permissionsPlain}

---
To jest automatyczna wiadomość z aplikacji Spotto.";

        await SendEmailAsync(childEmail, childBusinessName, subject, htmlBody, textBody);
    }
}