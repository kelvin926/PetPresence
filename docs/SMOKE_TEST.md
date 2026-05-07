# PetPresence Windows 런타임 스모크 테스트

## 1. 목적

이 스모크 테스트의 목적은 CI에서 확인하기 어려운 실제 Windows 데스크톱 런타임 동작을 사람이 짧게 확인하는 것입니다.

- Windows에서 PetPresence 서버와 데스크톱 앱이 실제로 실행되는지 확인합니다.
- 투명 overlay가 다른 앱의 포커스, 클릭, 일반 사용 흐름을 방해하지 않는지 확인합니다.
- 로컬 activity classification 결과가 own pet 상태 문구와 애니메이션에 반영되는지 확인합니다.
- SignalR server와 friend presence 라우팅이 동작하는지 확인합니다.

이 테스트는 기능 개발이나 자동 감시 테스트가 아닙니다. 화면 캡처, 키보드/마우스 hook, 프로세스 주입, 입력 주입 같은 침습적 검사는 수행하지 않습니다.

## 2. 사전 조건

- Windows 10 또는 Windows 11 실제 데스크톱 세션.
- .NET 8 SDK.
- PowerShell 또는 Windows Terminal.
- PetPresence repository clone.
- 선택 사항: 서버와 데스크톱 클라이언트를 나눠 실행할 두 개 이상의 terminal session.
- 선택 사항: 로컬 친구 시뮬레이션용 서로 다른 user ID 두 개. 예: `alice`, `bob`.

## 3. 기본 빌드 검증

Repository root에서 다음 명령을 실행합니다.

```powershell
dotnet restore
dotnet build -c Debug
dotnet test -c Debug --no-build
python scripts/verify_stage.py v0
python scripts/verify_stage.py v1
python scripts/verify_stage.py v2
python scripts/verify_stage.py v3
python scripts/verify_stage.py v4
```

`python` 명령이 없고 `python3`만 있는 환경에서는 같은 인자로 `python3`를 사용합니다.

기대 결과:

- restore/build/test가 예외 없이 성공합니다.
- xUnit regression tests가 `dotnet test`로 실제 실행됩니다.
- `verify_stage.py v0..v4`가 모두 `OK`를 출력합니다.
- privacy verifier가 약화되지 않았음을 확인합니다.

## 4. Server 실행

새 terminal에서 다음 명령을 실행합니다.

```powershell
dotnet run --project src/PetPresence.Server -c Debug
```

기대 결과:

- 서버가 예외 없이 시작합니다.
- console에 표시되는 listening URL을 확인합니다. 일반적으로 로컬 개발에서는 `http://localhost:5000` 같은 URL이 표시됩니다.
- SignalR hub endpoint `/presence`가 사용 가능합니다.
- friend endpoint가 사용 가능합니다.
  - `POST /friends/request`
  - `POST /friends/accept`
  - `POST /friends/block`
  - `GET /friends`

간단한 health check 예시:

```powershell
Invoke-RestMethod http://localhost:5000/health
```

친구 endpoint는 개발용 `X-User-Id` header를 사용합니다. 예:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/friends/request `
  -Headers @{ "X-User-Id" = "alice" } `
  -ContentType "application/json" `
  -Body '{"friendUserId":"bob"}'
```

서버 URL이 `http://localhost:5000`이 아니라면 console에 표시된 listening URL로 바꿉니다.

## 5. Desktop 단독 실행

서버 연결 없이 own pet overlay만 먼저 확인합니다.

CMD 예시:

```cmd
set PETPRESENCE_USER_ID=local-user
set PETPRESENCE_SERVER_URL=
dotnet run --project src/PetPresence.Desktop -c Debug
```

PowerShell 예시:

```powershell
$env:PETPRESENCE_USER_ID="local-user"
$env:PETPRESENCE_SERVER_URL=""
dotnet run --project src/PetPresence.Desktop -c Debug
```

기대 결과:

- 투명 overlay가 나타납니다.
- system tray icon이 나타납니다.
- own pet이 나타납니다.
- overlay가 focus를 가져가지 않습니다.
- normal mode에서는 click-through로 동작하여 다른 앱 클릭을 방해하지 않습니다.
- tray menu의 edit mode에서는 pet position dragging이 가능합니다.
- edit mode를 끄면 다시 다른 앱 조작을 방해하지 않습니다.

## 6. Local activity classification 확인

다음은 수동 확인 항목입니다. Classification은 foreground process/window title 기반의 근사값이므로 환경과 앱 제목에 따라 다를 수 있습니다.

- Word 또는 Notion이 있으면 문서 편집 화면을 foreground로 둡니다 → pet이 `문서 작성 중...` 상태를 표시하는지 확인합니다.
- VS Code를 foreground로 둡니다 → pet이 `코딩 중...` 상태를 표시하는지 확인합니다.
- 브라우저에서 YouTube 제목이 보이는 탭을 foreground로 둡니다 → pet이 `영상 보는 중...` 상태를 표시하는지 확인합니다.
- 브라우저 검색 결과 페이지를 foreground로 둡니다 → pet이 `웹서칭 중...` 상태를 표시하는지 확인합니다.
- idle threshold 동안 입력 없이 둡니다 → pet이 `자리 비움...` 상태를 표시하는지 확인합니다.

주의:

- Classification은 정확한 감시가 아니라 privacy-preserving approximation입니다.
- raw process name과 raw window title은 데스크톱 로컬 classifier 내부에서만 사용되어야 합니다.
- raw process name/window title을 서버 DTO나 SignalR payload로 보내면 안 됩니다.

## 7. Two-client friend presence 스모크 테스트

로컬에서 두 사용자를 시뮬레이션합니다.

1. 서버를 시작합니다.

   ```powershell
   dotnet run --project src/PetPresence.Server -c Debug
   ```

2. 클라이언트 A를 `alice`로 시작합니다.

   ```powershell
   $env:PETPRESENCE_USER_ID="alice"
   $env:PETPRESENCE_SERVER_URL="http://localhost:5000"
   dotnet run --project src/PetPresence.Desktop -c Debug
   ```

3. 다른 terminal에서 클라이언트 B를 `bob`으로 시작합니다.

   ```powershell
   $env:PETPRESENCE_USER_ID="bob"
   $env:PETPRESENCE_SERVER_URL="http://localhost:5000"
   dotnet run --project src/PetPresence.Desktop -c Debug
   ```

4. friend endpoint로 친구 요청/수락을 수행합니다.

   ```powershell
   Invoke-RestMethod `
     -Method Post `
     -Uri http://localhost:5000/friends/request `
     -Headers @{ "X-User-Id" = "alice" } `
     -ContentType "application/json" `
     -Body '{"friendUserId":"bob"}'

   Invoke-RestMethod `
     -Method Post `
     -Uri http://localhost:5000/friends/accept `
     -Headers @{ "X-User-Id" = "bob" } `
     -ContentType "application/json" `
     -Body '{"friendUserId":"alice"}'
   ```

5. accepted friend presence가 다른 pet으로 표시되는지 확인합니다.

6. 친구 관계가 아닌 사용자 presence는 표시되지 않아야 합니다. 필요하면 `charlie` 클라이언트를 별도 user ID로 실행하고, 친구 수락 전에는 `alice` 또는 `bob` overlay에 나타나지 않는지 확인합니다.

## 8. Privacy checks

스모크 테스트 중 다음을 확인합니다.

- 화면 캡처를 수행하지 않습니다.
- 키로깅 또는 global keyboard/mouse hook을 사용하지 않습니다.
- 브라우저 히스토리에 접근하지 않습니다.
- 문서 본문이나 채팅 내용을 읽지 않습니다.
- raw URL을 수집하지 않습니다.
- 다른 프로세스에 주입하거나 입력을 대신 주입하지 않습니다.
- server/shared DTO에는 raw process name 또는 raw window title이 없습니다.
- 서버는 canonical presence status, canonical status text, animation key, confidence, timestamp, 라우팅 identity만 받습니다.
- pause sharing, approximate status, excluded app 설정이 노출되어 있다면 해당 설정이 공유 억제/근사화에 반영되는지 확인합니다.

## 9. Pass/fail 체크리스트

| Item | Expected | Actual | Pass/Fail | Notes |
| --- | --- | --- | --- | --- |
| `dotnet restore` | 성공 |  |  |  |
| `dotnet build -c Debug` | 성공 |  |  |  |
| `dotnet test -c Debug --no-build` | xUnit tests 성공 |  |  |  |
| verifier v0..v4 | 모두 `OK` |  |  |  |
| server startup | 예외 없이 listening |  |  |  |
| `/health` | service/version 응답 |  |  |  |
| `/presence` SignalR hub | desktop client 연결 가능 |  |  |  |
| friend endpoints | request/accept/list 동작 |  |  |  |
| standalone desktop | own pet overlay 표시 |  |  |  |
| tray icon | 표시됨 |  |  |  |
| normal overlay mode | click-through, focus 방해 없음 |  |  |  |
| edit mode | pet 위치 drag 가능 |  |  |  |
| Word/Notion classification | `문서 작성 중...` 근사 표시 |  |  |  |
| VS Code classification | `코딩 중...` 근사 표시 |  |  |  |
| YouTube classification | `영상 보는 중...` 근사 표시 |  |  |  |
| browser search classification | `웹서칭 중...` 근사 표시 |  |  |  |
| idle classification | `자리 비움...` 표시 |  |  |  |
| accepted friend presence | 다른 pet으로 표시 |  |  |  |
| non-friend presence | 표시되지 않음 |  |  |  |
| privacy boundary | raw process/window title 서버 전송 없음 |  |  |  |

## 10. Known limitations

- Browser classification은 브라우저 title 기반 approximation입니다.
- Music detection은 Windows audio session behavior와 앱별 오디오 세션 노출 방식에 영향을 받을 수 있습니다.
- WPF overlay의 focus/click-through 동작은 CI만으로는 충분히 검증할 수 없으며 실제 Windows desktop session에서 확인해야 합니다.
- Installer/update flow는 이 smoke test에서 별도로 실행하지 않는 한 검증되지 않습니다.
- 이 문서는 runtime smoke test 절차이며, 기능 요구사항 승인이나 장기 QA 계획을 대체하지 않습니다.
