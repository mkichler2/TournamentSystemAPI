Tournament System API

API do zarządzania systemem turniejowym opartym na systemie pucharowym, zbudowany na .NET 8 i GraphQL (HotChocolate).

## Funkcjonalności

- **System pucharowy** - zarządzanie turniejami w formacie eliminacyjnym
- **Rejestracja i logowanie** - bezpieczna autentykacja użytkowników z hashowaniem haseł
- **JWT Authentication** - autoryzacja na podstawie tokenów JWT
- **GraphQL API** - pełne CRUD dla turniejów, meczów, uczestników
- **Pobieranie meczów użytkownika** - każdy zalogowany użytkownik może sprawdzić swoje mecze bez podawania ID
- **Publiczny dostęp do informacji** - każdy może tworzyć turnieje i mieć dostęp do wyników

## Wymagania wstępne

- .NET 8.0 SDK
- SQL Server (LocalDB lub pełna instancja)
- Narzędzie dotnet-ef (globalny tool - opcjonalnie)

## Instalacja i konfiguracja

### 1. Zainstaluj narzędzia Entity Framework (opcjonalnie)

Jeśli nie posiadasz globalnego narzędzia EF Core:

dotnet tool install --global dotnet-ef


### 2. Przygotuj bazę danych

Wykonaj reset bazy danych (usunięcie i ponowna migracja), aby zapewnić czyste środowisko testowe i zgodność ID:

dotnet ef database drop --force
dotnet ef database update

### 3. Uruchom aplikację

Przejdź do katalogu projektu i uruchom aplikację:

dotnet run --urls "http://localhost:5000"


Interfejs GraphQL (Banana Cake Pop) będzie dostępny pod adresem: **http://localhost:5000/graphql**

## Scenariusz testowy

> **Uwaga:** Po każdej operacji, będzie dostęp do danych bez autoryzacji. Aby testować operacje wymagające autoryzacji, ustaw token JWT w nagłówkach HTTP w Banana Cake Pop, dodając nagłówek `Authorization: Bearer <token>`.

### Krok 1: Rejestracja użytkowników

Rejestrujemy dwóch użytkowników do turnieju:


mutation Rejestracja {
  u1: register(firstName: "Maciej", lastName: "Maciejowski", email: "maciej@test.com", password: "123") {
    id
    firstName
    lastName
    email
  }
  u2: register(firstName: "Paweł", lastName: "Pawłowski", email: "pawel@game.com", password: "123") {
    id
    firstName
    lastName
    email
  }
}


**Oczekiwany wynik:** Dwaj użytkownicy ze znacznikami ID 1 i 2

### Krok 2: Logowanie użytkownika (JWT)

Użytkownik loguje się i otrzymuje token JWT na 7 dni:


mutation Logowanie {
  login(email: "maciej@test.com", password: "123")
}


**Oczekiwany wynik:** Token JWT - skopiuj ten token, będziesz go potrzebować w dalszych krokach dla operacji wymagających autoryzacji.

**Aby użyć tokenu w Banana Cake Pop:**
1. Otwórz ustawienia (ikona koła zębatego)
2. W sekcji "Headers" dodaj: `{"Authorization": "Bearer <TWÓJ_TOKEN>"}`
3. Zamknij ustawienia

### Krok 3: Utworzenie turnieju

Każdy użytkownik może utworzyć turniej (brak autoryzacji):


mutation UtworzTurniej {
  createTournament(name: "Mistrzostwa programowania", startDate: "2026-05-01T12:00:00Z") {
    id
    name
    status
    startDate
  }
}


**Oczekiwany wynik:** Turniej o ID 1 ze statusem "NotStarted"

### Krok 4: Przypisanie uczestników

Dodajemy obu użytkowników do turnieju:


mutation DodajUczestnikow {
  p1: addParticipant(tournamentId: 1, userId: 1) {
    id
    name
    status
  }
  p2: addParticipant(tournamentId: 1, userId: 2) {
    id
    name
    status
  }
}


**Oczekiwany wynik:** Turniej zawiera dwóch uczestników

### Krok 5: Generowanie drabinki

Automatyczne tworzenie struktury meczów na podstawie uczestników:


mutation GenerujDrabinke {
  generateBracket(tournamentId: 1) {
    id
    matches {
      id
      round
      player1 { 
        id
        firstName 
        lastName 
      }
      player2 { 
        id
        firstName 
        lastName 
      }
    }
  }
}


**Oczekiwany wynik:** Drabinka z jednym meczem rundy 1 między Maciejem a Pawłem

### Krok 6: Start turnieju

Zmiana statusu turnieju na "InProgress":


mutation Start {
  startTournament(id: 1) {
    id
    name
    status
  }
}


**Oczekiwany wynik:** Status turnieju zmieniony na "InProgress"

### Krok 7: Rozegranie meczu

Paweł (ID 2) wygrywa swój mecz:


mutation RozegrajMecz {
  playMatch(matchId: 1, winnerId: 2) {
    id
    round
    winner {
      id
      firstName
      lastName
    }
    player1 {
      id
      firstName
      lastName
    }
    player2 {
      id
      firstName
      lastName
    }
  }
}


**Oczekiwany wynik:** Mecz posiada zwycięzcę (Paweł)

### Krok 8: Pobranie meczów zalogowanego użytkownika (WYMAGA JWT)

**Upewnij się, że masz ustawiony token Macieja w nagłówkach (z kroku 2)!**

Zalogowany użytkownik pobiera informacje o swoich meczach bez podawania ID:
Jeżeli nie jest zalogowany, otrzymuje komunikat"The current user is not authorized to access this resource."


query MojeMecze {
  myMatches {
    id
    round
    player1 {
      id
      firstName
      lastName
    }
    player2 {
      id
      firstName
      lastName
    }
    winner {
      id
      firstName
      lastName
    }
  }
}


**Oczekiwany wynik:** Lista meczów użytkownika Macieja

### Krok 9: Pobieranie pełnej struktury turnieju

Każdy może pobrać publiczne informacje o wszystkich turniejach:


query SprawdzWyniki {
  tournaments {
    id
    name
    status
    bracket {
      id
      matches {
        id
        round
        player1 { 
          firstName 
          lastName 
        }
        player2 { 
          firstName 
          lastName 
        }
        winner {
          firstName
          lastName
        }
      }
    }
  }
}

**Oczekiwany wynik:** Kompletna struktura turnieju z meczami i wynikami


### Krok 11: Zakończenie turnieju

Zmiana statusu na "Finished":

mutation ZakonczTurniej {
  finishTournament(id: 1) {
    id
    name
    status
  }
}


**Oczekiwany wynik:** Status turnieju zmieniony na "Finished"

## Struktura Danych

Model bazy danych oparty na diagramie UML zawiera następujące encje:

- **User** - użytkownicy systemu (id, imię, nazwisko, email, hasło)
- **Tournament** - turnieje (id, nazwa, data rozpoczęcia, status)
- **Bracket** - drabinka turnieju (id, lista meczów)
- **Match** - poszczególne mecze (id, runda, gracz1, gracz2, zwycięzca)
- **TournamentParticipant** - relacja many-to-many między Tournament a User

## Bezpieczeństwo

- **Haszowanie haseł** - hasła przechowywane z użyciem `PasswordHasher<User>`
- **JWT Bearer** - tokeny ważne 7 dni
- **Autoryzacja GraphQL** - atrybut `[Authorize]` na metodach wymagających zalogowania
- **Bezpieczne pobieranie danych** - użytkownik pobiera tylko swoje dane na podstawie tokenu, nie musi podawać ID