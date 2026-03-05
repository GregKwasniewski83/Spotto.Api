using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Services;

public class EmailTemplateService
{
    public EmailTemplate GetWelcomeTemplate(string name)
    {
        return new EmailTemplate
        {
            Subject = "Witamy w PlaySpace!",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2E8B57; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .button {{ background: #2E8B57; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>PlaySpace</h1>
            <p>Platforma Sportowa</p>
        </div>
        <div class='content'>
            <h2>Witaj {name}!</h2>
            <p>Cieszymy się, że dołączyłeś do społeczności PlaySpace! 🎉</p>
            <p>Spotto to Twoja bramka do świata sportu i aktywności. Tutaj możesz:</p>
            <ul>
                <li>🏟️ Rezerwować obiekty sportowe</li>
                <li>👨‍🏫 Znajdować trenerów personalnych</li>
                <li>📅 Zarządzać harmonogramem treningów</li>
                <li>💳 Obsługiwać płatności bezpiecznie</li>
            </ul>
            <p>Zacznij już dziś i odkryj wszystkie możliwości!</p>
            <p>Sportowe pozdrowienia,<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Witaj {name}!

Cieszymy się, że dołączyłeś do społeczności PlaySpace!

Spotto to Twoja bramka do świata sportu i aktywności. Tutaj możesz:
- Rezerwować obiekty sportowe
- Znajdować trenerów personalnych  
- Zarządzać harmonogramem treningów
- Obsługiwać płatności bezpiecznie

Sportowe pozdrowienia,
Zespół Spotto"
        };
    }

    public EmailTemplate GetBusinessProfileCreatedTemplate(BusinessProfileDto profile)
    {
        return new EmailTemplate
        {
            Subject = "Profil biznesowy został utworzony! 🏢",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2E8B57; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .info-box {{ background: white; padding: 15px; margin: 10px 0; border-left: 4px solid #2E8B57; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🏢 Profil Biznesowy PlaySpace</h1>
        </div>
        <div class='content'>
            <h2>Gratulacje! Twój profil biznesowy został utworzony! 🎉</h2>
            <div class='info-box'>
                <h3>📋 Szczegóły profilu:</h3>
                <p><strong>Nazwa firmy:</strong> {profile.CompanyName}</p>
                <p><strong>Nazwa wyświetlana:</strong> {profile.DisplayName}</p>
                <p><strong>NIP:</strong> {profile.Nip}</p>
                <p><strong>Adres:</strong> {profile.Address}, {profile.City} {profile.PostalCode}</p>
            </div>
            {(string.IsNullOrEmpty(profile.TPayMerchantId) ? 
                "<p>⚠️ <em>Profil został utworzony bez integracji z systemem płatności TPay. Możesz skonfigurować płatności później w ustawieniach profilu.</em></p>" :
                $"<p>✅ <strong>Integracja z TPay została skonfigurowana!</strong><br>ID Sprzedawcy: {profile.TPayMerchantId}<br>Status weryfikacji: {profile.TPayVerificationStatus}</p>")}
            <p>Możesz teraz:</p>
            <ul>
                <li>📅 Zarządzać harmonogramem dostępności</li>
                <li>🏟️ Dodawać obiekty sportowe</li>
                <li>📊 Monitorować rezerwacje</li>
                <li>💰 Otrzymywać płatności</li>
            </ul>
            <p>Powodzenia w rozwijaniu biznesu!<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Gratulacje! Twój profil biznesowy został utworzony!

Szczegóły profilu:
- Nazwa firmy: {profile.CompanyName}
- Nazwa wyświetlana: {profile.DisplayName}
- NIP: {profile.Nip}
- Adres: {profile.Address}, {profile.City} {profile.PostalCode}

{(string.IsNullOrEmpty(profile.TPayMerchantId) ? 
    "Profil został utworzony bez integracji z systemem płatności TPay. Możesz skonfigurować płatności później w ustawieniach profilu." :
    $"Integracja z TPay została skonfigurowana!\nID Sprzedawcy: {profile.TPayMerchantId}\nStatus weryfikacji: {profile.TPayVerificationStatus}")}

Możesz teraz:
- Zarządzać harmonogramem dostępności
- Dodawać obiekty sportowe
- Monitorować rezerwacje
- Otrzymywać płatności

Powodzenia w rozwijaniu biznesu!
Zespół Spotto"
        };
    }

    public EmailTemplate GetTrainerProfileCreatedTemplate(TrainerProfileDto profile)
    {
        return new EmailTemplate
        {
            Subject = "Profil trenera został utworzony! 💪",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #FF6B35; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .info-box {{ background: white; padding: 15px; margin: 10px 0; border-left: 4px solid #FF6B35; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>💪 Profil Trenera Spotto</h1>
        </div>
        <div class='content'>
            <h2>Gratulacje! Twój profil trenera został utworzony! 🎉</h2>
            <div class='info-box'>
                <h3>📋 Szczegóły profilu:</h3>
                <p><strong>Nazwa:</strong> {profile.DisplayName}</p>
                <p><strong>Specjalizacje:</strong> {profile.Specializations}</p>
                <p><strong>Stawka godzinowa:</strong> {profile.HourlyRate:C}</p>
                <p><strong>Lata doświadczenia:</strong> {profile.ExperienceYears}</p>
            </div>
            {(string.IsNullOrEmpty(profile.TPayMerchantId) ? 
                "<p>⚠️ <em>Profil został utworzony bez integracji z systemem płatności TPay. Możesz skonfigurować płatności później w ustawieniach profilu.</em></p>" :
                $"<p>✅ <strong>Integracja z TPay została skonfigurowana!</strong><br>ID Sprzedawcy: {profile.TPayMerchantId}<br>Status weryfikacji: {profile.TPayVerificationStatus}</p>")}
            <p>Możesz teraz:</p>
            <ul>
                <li>📅 Zarządzać harmonogramem dostępności</li>
                <li>🏟️ Oferować treningi w obiektach</li>
                <li>👥 Prowadzić sesje treningowe</li>
                <li>💰 Otrzymywać płatności za usługi</li>
            </ul>
            <p>Powodzenia w rozwijaniu kariery trenerskiej!<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Gratulacje! Twój profil trenera został utworzony!

Szczegóły profilu:
- Nazwa: {profile.DisplayName}
- Specjalizacje: {profile.Specializations}
- Stawka godzinowa: {profile.HourlyRate:C}
- Lata doświadczenia: {profile.ExperienceYears}

{(string.IsNullOrEmpty(profile.TPayMerchantId) ? 
    "Profil został utworzony bez integracji z systemem płatności TPay. Możesz skonfigurować płatności później w ustawieniach profilu." :
    $"Integracja z TPay została skonfigurowana!\nID Sprzedawcy: {profile.TPayMerchantId}\nStatus weryfikacji: {profile.TPayVerificationStatus}")}

Możesz teraz:
- Zarządzać harmonogramem dostępności
- Oferować treningi w obiektach
- Prowadzić sesje treningowe
- Otrzymywać płatności za usługi

Powodzenia w rozwijaniu kariery trenerskiej!
Zespół Spotto"
        };
    }

    public EmailTemplate GetTPayRegistrationSuccessTemplate(string name, string merchantId, string activationLink)
    {
        return new EmailTemplate
        {
            Subject = "TPay - Rejestracja zakończona pomyślnie! 💳",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #0066CC; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .button {{ background: #0066CC; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block; margin: 10px 0; }}
        .success-box {{ background: #d4edda; border: 1px solid #c3e6cb; color: #155724; padding: 15px; border-radius: 4px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>💳 TPay Integration</h1>
        </div>
        <div class='content'>
            <div class='success-box'>
                <h2>✅ Rejestracja TPay zakończona pomyślnie!</h2>
            </div>
            <p>Cześć {name}!</p>
            <p>Twoje konto sprzedawcy TPay zostało pomyślnie utworzone i zintegrowane z PlaySpace.</p>
            <p><strong>ID Sprzedawcy:</strong> {merchantId}</p>
            {(!string.IsNullOrEmpty(activationLink) ? 
                $"<p><strong>Następny krok:</strong> Aktywacja konta</p><p>Aby zakończyć proces rejestracji, kliknij poniższy link i aktywuj swoje konto TPay:</p><p><a href='{activationLink}' class='button'>🔗 Aktywuj konto TPay</a></p>" :
                "<p>Twoje konto jest gotowe do użytku!</p>")}
            <p>Teraz możesz:</p>
            <ul>
                <li>💰 Otrzymywać płatności od klientów</li>
                <li>📊 Monitorować transakcje</li>
                <li>🔒 Korzystać z bezpiecznych płatności online</li>
            </ul>
            <p>Pozdrawiam,<br><strong>Zespół Spotto & TPay</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Rejestracja TPay zakończona pomyślnie!

Cześć {name}!

Twoje konto sprzedawcy TPay zostało pomyślnie utworzone i zintegrowane z PlaySpace.

ID Sprzedawcy: {merchantId}

{(!string.IsNullOrEmpty(activationLink) ? 
    $"Następny krok: Aktywacja konta\nAby zakończyć proces rejestracji, przejdź do: {activationLink}" :
    "Twoje konto jest gotowe do użytku!")}

Teraz możesz:
- Otrzymywać płatności od klientów
- Monitorować transakcje
- Korzystać z bezpiecznych płatności online

Pozdrawiam,
Zespół Spotto & TPay"
        };
    }

    public EmailTemplate GetTPayRegistrationFailureTemplate(string name, string errorMessage)
    {
        return new EmailTemplate
        {
            Subject = "TPay - Problemy z rejestracją ⚠️",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #DC3545; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .error-box {{ background: #f8d7da; border: 1px solid #f5c6cb; color: #721c24; padding: 15px; border-radius: 4px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⚠️ TPay Integration</h1>
        </div>
        <div class='content'>
            <div class='error-box'>
                <h2>❌ Wystąpił problem z rejestracją TPay</h2>
            </div>
            <p>Cześć {name}!</p>
            <p>Niestety, podczas rejestracji w systemie TPay wystąpił problem:</p>
            <p><strong>Błąd:</strong> {errorMessage}</p>
            <p><strong>Co dalej?</strong></p>
            <ul>
                <li>🔄 Spróbuj ponownie za kilka minut</li>
                <li>📞 Skontaktuj się z naszym wsparciem</li>
                <li>✅ Twój profil Spotto został utworzony pomyślnie</li>
            </ul>
            <p>Możesz dodać integrację z TPay później z poziomu ustawień profilu.</p>
            <p>Przepraszamy za niedogodności,<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Wystąpił problem z rejestracją TPay

Cześć {name}!

Niestety, podczas rejestracji w systemie TPay wystąpił problem:

Błąd: {errorMessage}

Co dalej?
- Spróbuj ponownie za kilka minut
- Skontaktuj się z naszym wsparciem
- Twój profil Spotto został utworzony pomyślnie

Możesz dodać integrację z TPay później z poziomu ustawień profilu.

Przepraszamy za niedogodności,
Zespół Spotto"
        };
    }

    // Reservation Templates
    public EmailTemplate GetReservationCreatedTemplate(string customerName, string facilityName, string date, string timeSlot, decimal amount)
    {
        return new EmailTemplate
        {
            Subject = "Rezerwacja potwierdzona!",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2E8B57; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .booking-details {{ background: white; padding: 15px; margin: 15px 0; border-radius: 4px; border-left: 4px solid #2E8B57; }}
        .button {{ background: #2E8B57; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Rezerwacja Potwierdzona</h1>
        </div>
        <div class='content'>
            <h2>Cześć {customerName}!</h2>
            <p>Twoja rezerwacja została pomyślnie potwierdzona! 🎉</p>
            <div class='booking-details'>
                <h3>📋 Szczegóły rezerwacji:</h3>
                <p><strong>Obiekt:</strong> {facilityName}</p>
                <p><strong>Data:</strong> {date}</p>
                <p><strong>Godzina:</strong> {timeSlot}</p>
                <p><strong>Koszt:</strong> {amount:C}</p>
            </div>
            <p>📍 <strong>Ważne:</strong></p>
            <ul>
                <li>Przybądź 15 minut przed rezerwacją</li>
                <li>Zabierz ze sobą wymagane dokumenty i odpowiedni sprzęt</li>
                <li>W razie problemów skontaktuj się z obiektem</li>
            </ul>
            <p>Życzymy udanego treningu!<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Rezerwacja potwierdzona!

Cześć {customerName}!

Twoja rezerwacja została pomyślnie potwierdzona!

Szczegóły rezerwacji:
- Obiekt: {facilityName}
- Data: {date}
- Godzina: {timeSlot}
- Koszt: {amount:C}

Ważne:
- Przybądź 15 minut przed rezerwacją
- Zabierz ze sobą wymagane dokumenty i odpowiedni sprzęt
- W razie problemów skontaktuj się z obiektem

Życzymy udanego treningu!
Zespół Spotto"
        };
    }

    public EmailTemplate GetReservationReminderTemplate(string customerName, string facilityName, string date, string timeSlot)
    {
        return new EmailTemplate
        {
            Subject = "Przypomnienie o jutrzejszej rezerwacji! ⏰",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #FF6B35; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .reminder-box {{ background: #fff3cd; border: 1px solid #ffeaa7; color: #856404; padding: 15px; border-radius: 4px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⏰ Przypomnienie o Rezerwacji</h1>
        </div>
        <div class='content'>
            <h2>Cześć {customerName}!</h2>
            <div class='reminder-box'>
                <h3>🗓️ Jutro masz zaplanowaną rezerwację!</h3>
                <p><strong>Obiekt:</strong> {facilityName}</p>
                <p><strong>Data:</strong> {date}</p>
                <p><strong>Godzina:</strong> {timeSlot}</p>
            </div>
            <p>📝 <strong>Pamiętaj:</strong></p>
            <ul>
                <li>✅ Sprawdź pogodę (dla obiektów zewnętrznych)</li>
                <li>Zabierz odpowiedni sprzęt sportowy</li>
                <li>👟 Załóż wygodną odzież sportową</li>
                <li>⏰ Przybądź 15 minut wcześniej</li>
            </ul>
            <p>Do zobaczenia na korcie!<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Przypomnienie o jutrzejszej rezerwacji! ⏰

Cześć {customerName}!

Jutro masz zaplanowaną rezerwację!

Szczegóły:
- Obiekt: {facilityName}
- Data: {date}
- Godzina: {timeSlot}

Pamiętaj:
- Sprawdź pogodę (dla obiektów zewnętrznych)
- Zabierz odpowiedni sprzęt sportowy
- Załóż wygodną odzież sportową
- Przybądź 15 minut wcześniej

Do zobaczenia na korcie!
Zespół Spotto"
        };
    }

    // Training Templates
    public EmailTemplate GetTrainingBookedTemplate(string participantName, string trainerName, string date, string timeSlot, decimal amount)
    {
        return new EmailTemplate
        {
            Subject = "Trening zarezerwowany! 💪",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #FF6B35; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .training-details {{ background: white; padding: 15px; margin: 15px 0; border-radius: 4px; border-left: 4px solid #FF6B35; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>💪 Trening Zarezerwowany</h1>
        </div>
        <div class='content'>
            <h2>Cześć {participantName}!</h2>
            <p>Twój trening personalny został zarezerwowany! 🎉</p>
            <div class='training-details'>
                <h3>🏋️ Szczegóły treningu:</h3>
                <p><strong>Trener:</strong> {trainerName}</p>
                <p><strong>Data:</strong> {date}</p>
                <p><strong>Godzina:</strong> {timeSlot}</p>
                <p><strong>Koszt:</strong> {amount:C}</p>
            </div>
            <p>💡 <strong>Przygotowanie do treningu:</strong></p>
            <ul>
                <li>Załóż wygodną odzież sportową</li>
                <li>Weź ze sobą ręcznik i wodę</li>
                <li>Poinformuj trenera o ewentualnych kontuzjach</li>
                <li>Przybądź 10 minut wcześniej</li>
            </ul>
            <p>Powodzenia na treningu!<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Trening zarezerwowany! 💪

Cześć {participantName}!

Twój trening personalny został zarezerwowany!

Szczegóły treningu:
- Trener: {trainerName}
- Data: {date}
- Godzina: {timeSlot}
- Koszt: {amount:C}

Przygotowanie do treningu:
- Załóż wygodną odzież sportową
- Weź ze sobą ręcznik i wodę
- Poinformuj trenera o ewentualnych kontuzjach
- Przybądź 10 minut wcześniej

Powodzenia na treningu!
Zespół Spotto"
        };
    }

    // Payment Templates
    public EmailTemplate GetPaymentSuccessfulTemplate(string customerName, string description, decimal amount, string transactionId)
    {
        return new EmailTemplate
        {
            Subject = "Płatność przetworzona pomyślnie! ✅",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #28a745; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .payment-details {{ background: white; padding: 15px; margin: 15px 0; border-radius: 4px; border-left: 4px solid #28a745; }}
        .success-badge {{ background: #d4edda; border: 1px solid #c3e6cb; color: #155724; padding: 10px; border-radius: 4px; text-align: center; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Płatność Potwierdzona</h1>
        </div>
        <div class='content'>
            <div class='success-badge'>
                <h3>💳 Płatność została przetworzona pomyślnie!</h3>
            </div>
            <h2>Cześć {customerName}!</h2>
            <p>Twoja płatność została zrealizowana poprawnie.</p>
            <div class='payment-details'>
                <h3>📋 Szczegóły płatności:</h3>
                <p><strong>Opis:</strong> {description}</p>
                <p><strong>Kwota:</strong> {amount:C}</p>
                <p><strong>ID Transakcji:</strong> {transactionId}</p>
                <p><strong>Status:</strong> Zrealizowano</p>
            </div>
            <p>Zachowaj ten email jako potwierdzenie płatności.</p>
            <p>Dziękujemy za wybór PlaySpace!<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Płatność przetworzona pomyślnie! ✅

Cześć {customerName}!

Twoja płatność została zrealizowana poprawnie.

Szczegóły płatności:
- Opis: {description}
- Kwota: {amount:C}
- ID Transakcji: {transactionId}
- Status: Zrealizowano

Zachowaj ten email jako potwierdzenie płatności.

Dziękujemy za wybór PlaySpace!
Zespół Spotto"
        };
    }

    // Email Verification Template
    public EmailTemplate GetEmailVerificationTemplate(string name, string webUrl, string deepLinkUrl)
    {
        return new EmailTemplate
        {
            Subject = "Zweryfikuj swój adres email - Spotto",
            HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2E8B57; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border-radius: 0 0 8px 8px; }}
        .button {{ color: white; padding: 14px 28px; text-decoration: none; border-radius: 4px; display: inline-block; margin: 10px 5px; font-weight: bold; }}
        .button-primary {{ background: #2E8B57; }}
        .button-secondary {{ background: #0066CC; }}
        .verification-box {{ background: white; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; border: 2px dashed #2E8B57; }}
        .warning {{ background: #fff3cd; border: 1px solid #ffeaa7; color: #856404; padding: 12px; border-radius: 4px; margin: 15px 0; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Spotto</h1>
            <p>Weryfikacja adresu email</p>
        </div>
        <div class='content'>
            <h2>Witaj {name}!</h2>
            <p>Dziękujemy za rejestrację w Spotto! Aby aktywować swoje konto, zweryfikuj swój adres email klikając w poniższy przycisk:</p>

            <div class='verification-box'>
                <a href='{webUrl}' class='button button-primary'>🌐 Weryfikuj w przeglądarce</a>
            </div>

            <div class='warning'>
                ⏰ <strong>Uwaga:</strong> Link weryfikacyjny jest ważny przez 7 dni. Po tym czasie będziesz musiał(a) poprosić o nowy link.
            </div>

            <p>Jeśli nie zakładałeś(aś) konta w Spotto, zignoruj tę wiadomość.</p>

            <p>Sportowe pozdrowienia,<br><strong>Zespół Spotto</strong></p>
        </div>
    </div>
</body>
</html>",
            TextBody = $@"Witaj {name}!

Dziękujemy za rejestrację w Spotto! Aby aktywować swoje konto, zweryfikuj swój adres email klikając w poniższy link:

Weryfikuj w przeglądarce: {webUrl}

UWAGA: Link weryfikacyjny jest ważny przez 7 dni. Po tym czasie będziesz musiał(a) poprosić o nowy link.

Jeśli nie zakładałeś(aś) konta w Spotto, zignoruj tę wiadomość.

Sportowe pozdrowienia,
Zespół Spotto"
        };
    }
}