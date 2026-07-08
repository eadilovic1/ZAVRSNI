# 🛍️ ePazar - Detaljan Opis Projekta

Dobrodošli u zvaničnu dokumentaciju projekta **ePazar**. Ovaj dokument pruža sveobuhvatan pregled arhitekture, strukture foldera i datoteka, kao i detaljan opis svih komponenti i funkcionalnosti sistema.

---

## 🏗️ Pregled Arhitekture Projekta

**ePazar** je moderna e-commerce web aplikacija razvijena pomoću **ASP.NET Core 9.0** frameworka, prateći **MVC (Model-View-Controller)** dizajn obrazac. Aplikacija omogućava korisnicima kupovinu i prodaju artikala, praćenje drugih korisnika, primanje e-mail i in-app notifikacija, administraciju korisničkih računa, te naprednu AI procjenu artikala.

### Ključne komponente sistema:
*   **Data Layer (EF Core):** Za rad sa bazom podataka koristi se Microsoft SQL Server u kombinaciji sa Entity Framework Core.
*   **Security & Auth (Identity):** ASP.NET Core Identity upravlja registracijom, prijavom, ulogama (Admin, Korisnik, Kurirska Služba) i ličnim podacima.
*   **AI Integration (OpenAI GPT-4o):** Integrisan je vještački finansijski savjetnik koji analizira isplativost artikala.
*   **PDF Generation (iText 7):** Omogućava korisnicima preuzimanje i izvoz njihovih ličnih podataka u PDF formatu.
*   **Email Services (SMTP):** Integracija za slanje email notifikacija o statusu narudžbi i novim artiklima.

---

## 📁 Stablo Foldera i Fajlova (Directory Tree)

Ispod je prikazana kompletna struktura projekta sa jasno naznačenim tipovima fajlova i ikonama za lakše snalaženje:

```text
ePazar/ (Root folder rješenja)
│
├── 📄 ooadepazar.sln                        [C# Solution fajl]
├── 📄 ooadepazar.sln.DotSettings.user       [JetBrains/ReSharper korisnička podešavanja]
│
└── 📁 ooadepazar/ (Glavni projekat)
    │
    ├── 📄 ooadepazar.csproj                 [C# Project - XML konfiguracija paketa i okruženja]
    ├── 📄 ooadepazar.csproj.user            [Visual Studio korisnički fajl]
    ├── 📄 Program.cs                        [C# Startup - Glavna ulazna tačka i konfiguracija servisa]
    ├── 📄 appsettings.json                  [JSON - Globalna konfiguracija aplikacije]
    ├── 📄 appsettings.Development.json      [JSON - Konfiguracija za razvojno okruženje]
    ├── 📄 ScaffoldingReadMe.txt             [Tekstualni fajl - Log ASP.NET generatora koda]
    │
    ├── 📁 .config/                          [Konfiguracioni folder alata]
    │
    ├── 📁 Properties/                       [Svojstva projekta i profili pokretanja]
    │   ├── 📁 PublishProfiles/              [Folder - Profili za objavu aplikacije]
    │   └── 📄 launchSettings.json           [JSON - Postavke IIS i Kestrel servera za lokalni pokret]
    │
    ├── 📁 Data/                             [Baza podataka i ORM sloj]
    │   └── 📄 ApplicationDbContext.cs       [C# - Glavni kontekst baze podataka, tabele i relacije]
    │
    ├── 📁 Migrations/                       [EF Core Migracije - Istorija izmjena baze podataka]
    │   ├── 📄 *__migration_name.cs          [C# - Skripte za izmjenu baze podataka]
    │   ├── 📄 *__migration_name.Designer.cs [C# - Metapodaci i dizajn kod migracija]
    │   └── 📄 ApplicationDbContextModelSnapshot.cs [C# - Trenutna slika stanja baze podataka]
    │
    ├── 📁 Interfaces/                       [Interfejsi i apstrakcije]
    │   └── 📄 klasa.cs                      [C# - Definicija IMailService interfejsa]
    │
    ├── 📁 Services/                         [Pozadinske usluge i servisi]
    │   └── 📄 MailService.cs                [C# - SMTP Implementacija za slanje emailova]
    │
    ├── 📁 Models/                           [Modeli podataka - Tabele u bazi i Enumi]
    │   ├── 📄 ApplicationUser.cs            [C# - Korisnički profil (proširuje IdentityUser)]
    │   ├── 📄 Artikal.cs                    [C# - Model za oglas/artikal]
    │   ├── 📄 Narudzba.cs                   [C# - Model za kupovinu i dostavu]
    │   ├── 📄 Notifikacija.cs               [C# - Model za in-app notifikacije]
    │   ├── 📄 Pracenje.cs                   [C# - Model za relaciju praćenja (Follow/Unfollow)]
    │   ├── 📄 ErrorViewModel.cs             [C# - Prikaz grešaka u aplikaciji]
    │   ├── 📄 Stanje.cs                     [C# Enum - Novo, Koristeno]
    │   ├── 📄 Status.cs                     [C# Enum - Kreiran, UObradi, Dostavljen]
    │   ├── 📄 Uloga.cs                      [C# Enum - Admin, Kurirska_Sluzba, Korisnik]
    │   └── 📁 ViewModels/                   [Pomoćni modeli za prenos podataka na View]
    │       └── 📄 KorisnikArtikliViewModel.cs [C# - Model za profilnu stranicu]
    │
    ├── 📁 Controllers/                      [MVC Kontroleri - Biznis logika i usmjeravanje]
    │   ├── 📄 HomeController.cs             [C# - Početna stranica, pretraga, filteri i AI integracija]
    │   ├── 📄 ArtikalController.cs          [C# - Upravljanje artiklima (CRUD)]
    │   ├── 📄 NarudzbaController.cs         [C# - Upravljanje narudžbama i dostavom]
    │   ├── 📄 NotifikacijaController.cs     [C# - Upravljanje in-app notifikacijama]
    │   ├── 📄 PracenjeController.cs         [C# - Logika praćenja korisnika (Follow/Unfollow)]
    │   ├── 📄 KorisnikController.cs         [C# - Prikaz profila i aktivnosti korisnika]
    │   ├── 📄 UserManagementController.cs   [C# - Admin panel za upravljanje korisnicima i ulogama]
    │   ├── 📄 MailController.cs             [C# API - Slanje email notifikacija putem API-ja]
    │   ├── 📄 OpenAIController.cs           [C# - Komunikacija sa OpenAI GPT-4o API modelom]
    │   └── 📄 ErrorController.cs            [C# - Rukovanje 404 i ostalim HTTP greškama]
    │
    ├── 📁 Areas/                            [ASP.NET Područja - Izolovane cjeline aplikacije]
    │   └── 📁 Identity/                     [Modul za autentifikaciju i registraciju]
    │       └── 📁 Pages/                    [Razor stranice sistema za registraciju i prijavu]
    │           ├── 📄 _Layout.cshtml        [Razor View - Izgled stranice za registraciju/prijavu]
    │           ├── 📄 _ValidationScriptsPartial.cshtml [Razor View - Klijentska validacija formi]
    │           ├── 📄 _ViewImports.cshtml   [Razor View - Uvoz direktiva za Identity stranice]
    │           ├── 📄 _ViewStart.cshtml     [Razor View - Globalne postavke dizajna za Identity]
    │           └── 📁 Account/              [Korisnički račun i sigurnost]
    │               ├── 📄 Login.cshtml & .cs [Razor i C# - Prijava na sistem]
    │               ├── 📄 Register.cshtml & .cs [Razor i C# - Registracija novih korisnika]
    │               ├── 📄 Logout.cshtml & .cs [Razor i C# - Odjava sa sistema]
    │               ├── 📄 ForgotPassword.cshtml & .cs [Razor i C# - Reset lozinke]
    │               ├── 📄 ...               [Ostale Identity datoteke (2FA, potvrde, blokade...)]
    │               └── 📁 Manage/           [Upravljanje profilom]
    │                   └── 📄 DownloadPersonalData.cshtml.cs [C# - Preuzimanje ličnih podataka u PDF formatu]
    │
    ├── 📁 Views/                            [MVC Prikazi - Korisnički interfejs (UI)]
    │   ├── 📄 _ViewStart.cshtml             [Razor View - Definiše osnovni layout za sve prikaze]
    │   ├── 📄 _ViewImports.cshtml           [Razor View - Globalni uvoz namespaces i tag helpera]
    │   ├── 📁 Home/
    │   │   └── 📄 Index.cshtml              [Razor View - Početni e-commerce dashboard sa artiklima]
    │   ├── 📁 Artikal/
    │   │   ├── 📄 Create.cshtml             [Razor View - Forma za objavu novog artikla]
    │   │   ├── 📄 Details.cshtml            [Razor View - Prikaz artikla sa AI recenzijom]
    │   │   ├── 📄 Edit.cshtml               [Razor View - Izmjena postojećeg artikla]
    │   │   ├── 📄 Delete.cshtml             [Razor View - Potvrda brisanja artikla]
    │   │   └── 📄 Index.cshtml              [Razor View - Admin lista artikala]
    │   ├── 📁 Narudzba/
    │   │   ├── 📄 Create.cshtml             [Razor View - Forma za kreiranje narudžbe i odabir kurira]
    │   │   ├── 📄 Details.cshtml            [Razor View - Detaljan pregled narudžbe za kupca/kurira]
    │   │   ├── 📄 Edit.cshtml               [Razor View - Izmjena podataka o narudžbi]
    │   │   ├── 📄 Delete.cshtml             [Razor View - Brisanje narudžbe]
    │   │   └── 📄 Index.cshtml              [Razor View - Lista narudžbi za kurirske službe i admine]
    │   ├── 📁 Notifikacija/
    │   │   ├── 📄 Index.cshtml              [Razor View - Lista svih primljenih notifikacija]
    │   │   └── 📄 Details.cshtml, Create, Edit, Delete [Razor Views - CRUD notifikacija]
    │   ├── 📁 Korisnik/
    │   │   └── 📄 Index.cshtml              [Razor View - Javni profil sa oglasima i istorijom kupovine]
    │   ├── 📁 UserManagement/
    │   │   ├── 📄 Index.cshtml              [Razor View - Tabela korisnika i opcija za brisanje]
    │   │   └── 📄 EditRoles.cshtml          [Razor View - Dodjeljivanje uloga korisnicima (npr. Kurir)]
    │   └── 📁 Shared/
    │       ├── 📄 _Layout.cshtml            [Razor View - Glavni navigacioni i strukturni šablon sajta]
    │       ├── 📄 _Layout.cshtml.css        [CSS - Izolovani stilovi navigacionog menija]
    │       ├── 📄 _LoginPartial.cshtml      [Razor View - Kontrola za prijavu/registraciju u zaglavlju]
    │       ├── 📄 Error.cshtml              [Razor View - Prikaz neočekivanih grešaka u radu]
    │       ├── 📄 NotFound.cshtml           [Razor View - Prikaz stranice za 404 greške]
    │       └── 📄 _ValidationScriptsPartial.cshtml [Razor View - Uvoz skripti za validaciju formi]
    │
    └── 📁 wwwroot/                          [Javni statički fajlovi]
        ├── 📄 favicon.ico                   [Ikona - Favicon sajta]
        ├── 📁 css/
        │   └── 📄 site.css                  [CSS - Stilovi dizajna, boja i responzivnosti]
        ├── 📁 js/
        │   └── 📄 site.js                   [JavaScript - Klijentska logika aplikacije]
        └── 📁 lib/                          [Lokalne klijentske biblioteke - Bootstrap, jQuery, Validation]
```

---

## 🔍 Detaljna Analiza Foldera i Datoteka

### 1. Root Datoteke i Konfiguracija
*   **`ooadepazar.sln`**: Datoteka rješenja (Solution file) koja grupiše i upravlja C# projektom unutar Visual Studio/Rider okruženja.
*   **`ooadepazar.csproj`**: Projekat fajl koji sadrži sve NuGet pakete, konfiguracije ciljnog frameworka (`.net9.0`) i klijentska podešavanja. Ovdje su uvezene ključne biblioteke kao što su **iText 7** za PDF, **Markdig** za parsiranje Markdowna, te **EF Core SqlServer**.
*   **`Program.cs`**: Srce aplikacije. Tu se registruju svi servisi (Dependency Injection) i definiše redoslijed izvršavanja middleware-a (Routing, Static Files, Authentication, Authorization).
*   **`appsettings.json`**: Konfiguracioni fajl koji sadrži osjetljive i sistemske podatke kao što su **konekcijski string za bazu podataka (`DefaultConnection`)**, postavke e-mail servera (**SMTP**) i konfiguracija logovanja.

---

### 2. Models (Modeli podataka i Enumi)
Modeli predstavljaju strukturu tabela unutar baze podataka:
*   **`ApplicationUser.cs`**: Proširuje podrazumijevani ASP.NET Core `IdentityUser`. Sadrži dodatna polja specifična za ePazar: `Ime`, `Prezime`, `Adresa`, `DatumRegistracije`, `BrojTelefona`, te naziv kurirske službe (`KurirskaSluzba`) ukoliko je korisnik registrovan kao kurir.
*   **`Artikal.cs`**: Predstavlja proizvod koji je oglašen na ePazaru. Sadrži atribute poput `Naziv`, `Stanje` (Enum), `Opis`, `Cijena`, `Lokacija`, `SlikaUrl`, `Kategorija` (Enum), `DatumObjave`, `DatumAzuriranja`, `Narucen` (oznaka da li je artikal kupljen), te vezu sa vlasnikom artikla (`Korisnik`).
*   **`Narudzba.cs`**: Modelira proces kupovine artikla. Bilježi `DatumNarudzbe`, `DatumObrade`, `Status` narudžbe, te povezuje kupca (`Korisnik`), kupljeni proizvod (`Artikal`) i dostavljača (`KurirskaSluzba`).
*   **`Notifikacija.cs`**: Predstavlja in-app obavještenje. Sadrži HTML tekst (`Sadrzaj`), datum kreiranja, oznaku `Procitana` i primaoca (`KorisnikId`).
*   **`Pracenje.cs`**: Implementira društvenu mrežu unutar platforme. Povezuje pratioca (`PratilacID`) sa korisnikom koji se prati (`PraceniID`).
*   **Enumi (`Stanje.cs`, `Status.cs`, `Uloga.cs`, `Kategorija.cs`)**:
    *   `Stanje`: Novo, Koristeno
    *   `Status`: Kreiran, UObradi, Dostavljen
    *   `Uloga`: Admin, Kurirska_Sluzba, Korisnik
    *   `Kategorija`: Sadrži 16 različitih e-commerce kategorija (Elektronika, Odjeća, Obuća, Knjige...).

---

### 3. Controllers (Biznis Logika)
Kontroleri primaju korisničke zahtjeve, obrađuju ih u saradnji sa bazom podataka i vraćaju odgovarajuće prikaze (Views):
*   **`HomeController.cs`**: Upravlja početnom stranicom. Uključuje kompletnu pretragu, filtriranje po kategorijama, sortiranje (Najnovije, Cijena rastuće/opadajuće, Naziv A-Z). Takođe sadrži metodu `GetAIResponseInMarkdown` koja poziva OpenAI servis za recenziju artikla i renderuje rezultat kao HTML na korisničkom interfejsu.
*   **`ArtikalController.cs`**: Sadrži CRUD operacije nad oglasima. Prilikom dodavanja novog artikla, automatski pronalazi sve pratioce tog prodavača, kreira za njih in-app notifikaciju, te šalje email sa informacijom o novom artiklu.
*   **`NarudzbaController.cs`**: Upravlja kreiranjem narudžbi (gdje kupac bira lokaciju dostave i željenu kurirsku službu). Takođe omogućava kuririma i adminima da ažuriraju status narudžbe (`PromijeniStatus`), što automatski obavještava kupca putem emaila.
*   **`PracenjeController.cs`**: Sadrži akcije `Follow` i `Unfollow`. Kada korisnik zaprati nekoga, automatski se kreira in-app notifikacija za praćenog korisnika.
*   **`UserManagementController.cs`**: Dostupan isključivo administratorima. Omogućava pregled svih registrovanih korisnika, izmjenu njihovih uloga (npr. postavljanje korisnika za Kurirsku Službu) te potpuno brisanje korisnika. Prilikom brisanja koristi se kaskadno čišćenje koje sigurno briše sve artikle, notifikacije i narudžbe povezane sa tim korisnikom bez narušavanja integriteta baze podataka.
*   **`NotifikacijaController.cs`**: Omogućava korisnicima pregled svih svojih notifikacija, detalje i označavanje notifikacija kao pročitanih.
*   **`OpenAIController.cs`**: Pomoćni kontroler koji komunicira sa OpenAI API-jem. Šalje specifičan sistemski prompt koji upućuje GPT-4o model da se ponaša kao profesionalni finansijski menadžer koji artikle ocjenjuje kroz 4 sekcije (dobre strane, loše strane, poređenje sa tržištem i zaključak o isplativosti).
*   **`MailController.cs`**: API kontroler za slanje emailova.
*   **`ErrorController.cs`**: Usmjerava korisnike na custom dizajniranu 404 NotFound stranicu u slučaju nepostojećih ruta.

---

### 4. Data i Migrations (EF Core i Baza podataka)
*   **`ApplicationDbContext.cs`**: Konfiguracioni fajl baze podataka. Mapira modele u SQL tabele (`Artikal`, `Narudzba`, `Notifikacija`, `Pracenje`). Definiše ponašanje kod brisanja (Cascade za brisanje artikala kod uklanjanja korisnika, te Restrict na nivou kurira da bi se spriječilo slučajno brisanje aktivnih dostava).
*   **`Migrations/`**: Sadrži automatski generisane C# skripte koje prate istoriju izmjena baze podataka i omogućavaju lako ažuriranje ili vraćanje baze na starije verzije.

---

### 5. Services & Interfaces (Vanjske integracije i SMTP)
*   **`Interfaces/klasa.cs`**: Sadrži interfejs `IMailService` koji garantuje postojanje asinhronog metoda `SendEmailAsync`.
*   **`Services/MailService.cs`**: Konkretna SMTP implementacija koja čita SMTP host, port, username i password iz `appsettings.json` i šalje email notifikacije.
*   **iText PDF Izvoz (`DownloadPersonalData.cshtml.cs`)**: Nalazi se unutar Identity Area. Korištenjem **iText 7** kreira se strukturiran PDF dokument sa ličnim podacima korisnika, koristeći Helvetica font sa podrškom za lokalne karaktere (`Cp1250` kodna stranica), tabelarni prikaz i zaglavlja.

---

### 6. Views (Korisnički Interfejs - UI)
Prikazi su pisani u **Razor (HTML + C#)** sintaksi i podijeljeni su po kontrolerima:
*   **`Shared/_Layout.cshtml`**: Glavni šablon koji sadrži zaglavlje (navbar sa pretragom, linkovima na profile, notifikacijama i brojačem nepročitanih obavještenja) i podnožje (footer) aplikacije.
*   **`Home/Index.cshtml`**: Izuzetno responzivan grid sa karticama artikala, cijenom, stanjem, lokacijom i brzim akcijama za kupovinu ili detaljan pregled.
*   **`Artikal/Details.cshtml`**: Detaljna stranica proizvoda koja nudi opciju aktivacije **AI Pomoćnika** koji u realnom vremenu učitava i prikazuje detaljnu analizu isplativosti artikla.
*   **`Narudzba/Index.cshtml`**: Prikaz namijenjen kuririma i adminima. Omogućava im filtriranje dostava po kurirskim službama i brzu promjenu statusa u realnom vremenu jednim klikom (npr. iz "Kreiran" u "U Obradi" ili "Dostavljen").
*   **`UserManagement/Index.cshtml`**: Administrativna tabela sa opcijama za upravljanje korisnicima i njihovim ulogama.

---

### 7. wwwroot (Statički resursi)
*   **`css/site.css`**: Sadrži sve prilagođene stilove, teme, harmonizovane palete boja, responzivnost za mobilne uređaje i dizajnerske efekte poput glassmorphism-a i glatkih animacija pri prelazima.
*   **`js/site.js`**: Implementira klijentske skripte za bržu interakciju, rukovanje notifikacijama i asinhrono učitavanje AI odgovora bez osvježavanja cijele stranice.

---

## 🌟 Ključni Tokovi u Aplikaciji

1.  **Tok kupovine**: Korisnik pronalazi artikal -> Klikće na "Naruči" -> Otvara se forma za unos lokacije i odabir dostupne kurirske službe -> Kreira se narudžba sa statusom `Kreiran` -> Artikal se označava kao `Narucen` i uklanja iz javne pretrage -> Vlasnik artikla dobija in-app notifikaciju.
2.  **Tok praćenja i obavještenja**: Korisnik A zaprati Korisnika B -> Korisnik B dobija notifikaciju. Kada Korisnik B objavi novi artikal, svi njegovi pratioci (uključujući Korisnika A) automatski dobijaju in-app notifikaciju u realnom vremenu, kao i personalizovanu email poruku.
3.  **Dostava artikla**: Kurir se prijavljuje -> Vidi sve narudžbe dodijeljene svojoj kurirskoj službi -> Klikne na "U Obradi" prilikom preuzimanja -> Klikne na "Dostavljen" prilikom predaje artikla -> Kupac automatski dobija email obavještenje o svakoj promjeni statusa.
4.  **AI Analiza**: Na stranici detalja artikla, kupac može kliknuti na AI dugme -> Sistem šalje prompt OpenAI GPT-4o modelu -> Dobijeni strukturirani Markdown se dinamički prevodi u HTML preko `Markdig` i prikazuje kupcu kao profesionalni finansijski izvještaj o isplativosti kupovine.

---
*Dokument kreiran u sklopu detaljne analize projekta ePazar.*
