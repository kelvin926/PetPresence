구현 방향은 **“Windows 전용 데스크톱 펫 + 로컬 활동 분류기 + 실시간 presence 서버”**로 잡는 게 가장 안정적입니다.
Codex Pet처럼 “작은 오버레이가 현재 상태를 보여주는 구조”는 맞지만, Codex Pet은 Codex 작업 상태를 보여주는 것이고, 현서님 앱은 **사용자 PC의 foreground activity를 로컬에서 분류한 뒤 친구에게 공유**하는 점이 다릅니다. Codex 공식 문서에서도 pets는 다른 앱을 쓰는 동안 활성 작업 상태를 보여주는 floating overlay로 설명됩니다. ([OpenAI 개발자][1])

## 1. 추천 기술 스택

### 가장 추천: **C# WPF + ASP.NET Core SignalR**

Windows 전용이고 “현재 활성 프로세스 확인”이 핵심이라면 **C# WPF**가 제일 깔끔합니다.

이유는 명확합니다.

* Windows API를 P/Invoke로 직접 호출하기 쉽습니다.
* 투명/최상단/클릭 통과 오버레이를 만들기 쉽습니다.
* Electron보다 가볍고, native API binding 문제가 적습니다.
* 서버까지 .NET으로 맞추면 SignalR 클라이언트 연동이 단순합니다.

구성은 이렇게 가면 됩니다.

```text
PetPresence/
  desktop/
    PetPresence.Desktop/        # WPF 앱
      Overlay/
      Activity/
      Presence/
      Assets/
  server/
    PetPresence.Server/         # ASP.NET Core + SignalR
      Hubs/
      Auth/
      Friends/
      Presence/
  shared/
    PetPresence.Contracts/      # DTO, enum, protocol model
```

SignalR는 .NET 앱에서 hub와 통신할 수 있는 클라이언트 패키지를 제공하고, WPF 샘플도 공식 문서에 포함되어 있습니다. 친구별 상태 전송에는 SignalR의 user/group 전송 모델이 잘 맞습니다. ([Microsoft Learn][2])

대안으로 **Electron + TypeScript**도 가능하지만, Windows foreground process 감지에서 native addon/ABI 문제가 생길 수 있습니다. UI는 편하지만 “프로세스 감지 안정성”이 핵심인 앱에는 WPF 쪽이 낫습니다. Tauri/Rust도 좋지만 MVP에는 난도가 올라갑니다.

---

## 2. 전체 아키텍처

```text
[내 PC]
  ActivityDetector
    └─ 현재 foreground window 확인
    └─ PID / process name / window title 수집
    └─ idle time / audio session 선택적으로 확인

  ActivityClassifier
    └─ chrome + YouTube title → 영상 보는 중
    └─ WINWORD / HWP / Notion / Obsidian → 문서 작성 중
    └─ browser 일반 상태 → 웹서칭 중
    └─ Spotify / audio session → 음악 듣는 중
    └─ 일정 시간 입력 없음 → 자리 비움

  OverlayRenderer
    └─ 내 친구들의 펫 표시
    └─ 말풍선 표시
    └─ 클릭 통과 / 최상단 / 투명 창

  PresenceClient
    └─ 내 상태를 서버에 전송
    └─ 친구 상태를 수신

[서버]
  Auth
  FriendGraph
  PresenceHub
  PresenceTTL
```

핵심은 **활동 감지는 로컬에서 끝내고, 서버에는 “분류된 상태”만 보내는 것**입니다.
예를 들어 서버로 `chrome.exe`, `논문초안.docx - Word`, `고려대학교 포털` 같은 원본 정보를 보내면 프라이버시 리스크가 큽니다. 기본값은 반드시 아래처럼 해야 합니다.

```json
{
  "userId": "hyunseo",
  "status": "WritingDocument",
  "statusText": "문서 작성 중...",
  "animation": "typing",
  "confidence": 0.82,
  "lastSeenAt": "2026-05-07T10:12:30Z"
}
```

---

## 3. “현재 뭐 하는지” 감지 방식

여기서 중요한 판단이 있습니다.
**“켜져 있는 프로세스 전체”를 보면 안 되고, foreground window를 봐야 합니다.**

예를 들어 Chrome, Word, Spotify, VS Code가 모두 켜져 있으면 “켜진 프로세스”만으로는 현재 뭘 하는지 알 수 없습니다. Windows의 `GetForegroundWindow()`는 사용자가 현재 작업 중인 foreground window handle을 반환하고, `GetWindowThreadProcessId()`는 그 window를 만든 process ID를 얻는 데 쓸 수 있습니다. ([Microsoft Learn][3])

감지 로직은 이 순서가 좋습니다.

```text
1. GetForegroundWindow()
2. GetWindowThreadProcessId(hwnd)
3. PID → process name
4. hwnd → window title
5. 내 앱 자신이면 무시
6. idle time 확인
7. rule-based classifier 적용
8. 상태가 3~5초 이상 유지될 때만 확정
9. 서버에는 상태 enum만 전송
```

### C# 감지 스켈레톤

Codex에게 이 정도 구조를 기준으로 구현시키면 됩니다.

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public sealed record ForegroundAppSnapshot(
    int ProcessId,
    string ProcessName,
    string WindowTitle,
    DateTimeOffset CapturedAt
);

public static class ForegroundWindowReader
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    public static ForegroundAppSnapshot? Read()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == Environment.ProcessId)
            return null;

        string processName;
        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            return null;
        }

        var titleBuffer = new StringBuilder(512);
        GetWindowText(hwnd, titleBuffer, titleBuffer.Capacity);

        return new ForegroundAppSnapshot(
            ProcessId: (int)pid,
            ProcessName: processName,
            WindowTitle: titleBuffer.ToString(),
            CapturedAt: DateTimeOffset.UtcNow
        );
    }
}
```

이 코드는 다른 프로그램을 조작하지 않고, 현재 활성 window와 process 정보를 읽기만 합니다. 단, “프로그램에 절대 아무 영향도 없음”을 더 엄밀하게 보장하려면 **키보드 후킹, 마우스 후킹, 화면 캡처, UI Automation으로 컨트롤 내부 텍스트 읽기, 브라우저 히스토리 접근, URL 강제 추출**은 MVP에서 빼야 합니다.

---

## 4. 상태 분류 규칙

처음부터 AI/ML로 분류하지 말고 **rule-based classifier**로 시작하는 게 맞습니다. 상태 수가 적고, 오류가 발생해도 디버깅이 쉽습니다.

예시:

```csharp
public enum ActivityKind
{
    Unknown,
    Away,
    WebBrowsing,
    WatchingVideo,
    WritingDocument,
    ListeningMusic,
    Coding,
    Gaming
}

public sealed record ActivityState(
    ActivityKind Kind,
    string StatusText,
    string AnimationKey,
    double Confidence
);
```

분류 규칙 예시는 다음과 같습니다.

```text
idle > 5분
  → Away / "자리 비움..."

process in [WINWORD, HWP, Notion, Obsidian, Typora]
  → WritingDocument / "문서 작성 중..."

process in [Code, devenv, pycharm64, rider64]
  → Coding / "코딩 중..."

process in [Spotify, AppleMusic, MusicBee, foobar2000]
  → ListeningMusic / "음악 듣는 중..."

browser + title contains [YouTube, Netflix, Twitch, Disney+, 웨이브, 티빙]
  → WatchingVideo / "영상 보는 중..."

browser + title contains [Google, Bing, Naver, 검색]
  → WebBrowsing / "웹서칭 중..."

browser only
  → WebBrowsing / "웹 보는 중..."
```

주의할 점은 브라우저입니다.
프로세스명이 `chrome.exe`라고 해서 YouTube인지, Google Docs인지, 논문 검색인지 알 수 없습니다. MVP에서는 **window title 기반 추정**으로 충분합니다. 정확도를 높이고 싶으면 나중에 선택형 브라우저 확장을 만들어 active tab URL을 로컬 앱에 보내게 할 수 있지만, 이건 프라이버시 부담이 크므로 기본 기능으로 넣지 않는 편이 좋습니다.

음악도 비슷합니다. Spotify가 백그라운드에서 재생 중인데 foreground는 Word일 수 있습니다. “진짜 음악 듣는 중”을 더 잘 잡고 싶으면 Windows Core Audio/WASAPI의 audio session을 읽어 재생 중인 PID를 추정하는 기능을 나중에 추가하면 됩니다. Microsoft 문서상 WASAPI는 앱과 오디오 endpoint 사이의 오디오 흐름을 관리하는 API이고, audio session manager 계열 인터페이스로 세션을 다룰 수 있습니다. ([Microsoft Learn][4])

---

## 5. 오버레이 구현 방식

친구가 여러 명이면 **펫마다 별도 window를 만들기보다는, 하나의 투명 topmost window 안에 여러 pet control을 배치**하는 게 좋습니다.

```text
OverlayWindow
  Canvas
    FriendPetControl: 현서
    FriendPetControl: 친구A
    FriendPetControl: 친구B
```

이 방식이 좋은 이유는 다음과 같습니다.

* window z-order 관리가 단순합니다.
* 다중 모니터/DPI 대응이 쉽습니다.
* 말풍선이 잘리지 않게 layout 계산하기 쉽습니다.
* click-through 처리를 한 번만 하면 됩니다.
* 여러 펫이 떠 있어도 성능 부담이 작습니다.

WPF에서 투명 window를 만들려면 `AllowsTransparency=true`와 `WindowStyle=None` 조합이 필요합니다. WPF의 `Topmost=true`는 topmost z-order에 올리는 방식입니다. ([Microsoft Learn][5])

### Normal mode와 Edit mode를 분리하세요

이 앱은 실행 중인 프로그램에 영향을 주면 안 되므로 기본은 **click-through mode**여야 합니다.

```text
Normal mode
  - 펫 표시
  - 말풍선 표시
  - 마우스 클릭은 아래 앱으로 통과
  - 포커스 가져오지 않음

Edit mode
  - 사용자가 펫 위치를 드래그 가능
  - 설정 UI 열기 가능
  - 이때만 click-through 해제
```

Windows의 layered window에서 `WS_EX_TRANSPARENT`가 설정되면 마우스 이벤트가 아래 window로 전달됩니다. Microsoft 문서도 layered window에 `WS_EX_TRANSPARENT`가 있으면 mouse events가 아래 window로 전달된다고 설명합니다. ([Microsoft Learn][6])

Codex에게는 이렇게 요구하면 됩니다.

```text
OverlayWindow는 기본 상태에서 클릭을 가로채면 안 된다.
WS_EX_LAYERED, WS_EX_TRANSPARENT, WS_EX_TOOLWINDOW, WS_EX_NOACTIVATE를 적용한다.
단, LayoutEditMode=true일 때만 WS_EX_TRANSPARENT를 제거해서 펫을 드래그할 수 있게 한다.
```

---

## 6. 펫 애니메이션 설계

처음에는 Live2D까지 갈 필요 없습니다. MVP는 **spritesheet**가 제일 좋습니다.

```text
assets/pets/cat/
  idle.png
  typing.png
  watching.png
  browsing.png
  listening.png
  away.png
  offline.png
  manifest.json
```

`manifest.json` 예시:

```json
{
  "petId": "cat_default",
  "frameWidth": 96,
  "frameHeight": 96,
  "fps": {
    "idle": 6,
    "typing": 10,
    "watching": 4,
    "browsing": 6,
    "listening": 8,
    "away": 2,
    "offline": 1
  }
}
```

말풍선은 상태별로 짧게 유지하세요.

```text
문서 작성 중...
영상 보는 중...
웹서칭 중...
음악 듣는 중...
코딩 중...
자리 비움...
오프라인...
```

중요한 UI 디테일은 **말풍선을 너무 자주 바꾸지 않는 것**입니다. foreground app은 사용자가 alt-tab만 해도 계속 바뀌므로, 1초마다 말풍선이 바뀌면 산만합니다.

추천 정책:

```text
감지는 1초마다
상태 확정은 동일 상태 3~5초 유지 후
서버 전송은 상태 변경 시 또는 20~30초 heartbeat
말풍선 최소 표시 시간 5초
친구 offline 판정은 lastSeen 60~90초 초과
```

---

## 7. Presence 서버 설계

서버는 복잡할 필요 없습니다. 핵심은 **친구 관계가 승인된 사용자에게만 상태를 보내는 것**입니다.

### 데이터 모델

```csharp
public enum PresenceStatus
{
    Offline,
    Away,
    WebBrowsing,
    WatchingVideo,
    WritingDocument,
    ListeningMusic,
    Coding,
    Unknown
}

public sealed record PresenceUpdateDto(
    string UserId,
    PresenceStatus Status,
    string StatusText,
    string AnimationKey,
    double Confidence,
    DateTimeOffset UpdatedAt
);
```

### DB 테이블

```text
users
  id
  display_name
  pet_id
  created_at

friendships
  requester_id
  addressee_id
  status        # pending / accepted / blocked
  created_at

presence_snapshots
  user_id
  status
  status_text
  animation_key
  confidence
  updated_at
  expires_at
```

MVP에서는 `presence_snapshots`를 DB에 꼭 저장하지 않고 서버 메모리 + TTL로 처리해도 됩니다. 다만 앱을 껐다 켰을 때 마지막 상태를 보여주고 싶으면 DB에 저장하세요.

### SignalR Hub 개념

```text
Client → Server
  UpdatePresence(PresenceUpdateDto update)

Server → Friends
  FriendPresenceChanged(PresenceUpdateDto update)

Client → Server
  SetPet(string petId)
  SetPrivacyMode(...)
```

친구 상태 전송 흐름:

```text
1. 사용자가 SignalR 연결
2. 서버가 JWT에서 userId 확인
3. user:{userId} group에 연결 추가
4. 사용자가 UpdatePresence 호출
5. 서버가 accepted friends 조회
6. 각 친구의 user group으로 FriendPresenceChanged 전송
```

---

## 8. 프라이버시 기준은 강하게 잡는 게 좋습니다

이 앱은 귀여운 형태지만 본질은 **presence sharing / activity sharing**입니다. 잘못 만들면 감시 앱처럼 보일 수 있습니다.

최소한 아래 원칙은 넣는 게 좋습니다.

```text
기본값:
  서버에는 processName/windowTitle을 보내지 않는다.
  로컬에서 분류한 status enum만 보낸다.

사용자 설정:
  상태 공유 일시정지
  특정 앱 감지 제외
  특정 시간대 공유 끄기
  친구별 공유 허용/차단
  "항상 오프라인으로 보이기"
  "대략적 상태만 공유" 모드

금지:
  키로깅
  화면 캡처
  브라우저 히스토리 수집
  문서 내용 읽기
  채팅 내용 읽기
  URL 원문 전송
  친구 몰래 자동 실행/숨김 실행
```

특히 `windowTitle`에는 문서명, 유튜브 영상 제목, 검색어, 회의명, 논문 제목이 들어갈 수 있습니다. 디버그 로그에도 원본 title을 남기지 않는 편이 안전합니다.

---

## 9. MVP 개발 순서

### v0: 로컬 감지 + 내 펫만 표시

목표는 네트워크 없이 “내 PC에서 내 펫이 현재 상태에 따라 바뀌는 것”입니다.

```text
- WPF transparent overlay
- system tray icon
- foreground process detector
- rule-based classifier
- 내 pet animation state 변경
- 말풍선 표시
- click-through normal mode
- edit mode에서 위치 이동
```

이 단계가 가장 중요합니다. 여기서 프로세스 감지와 오버레이 안정성이 잡혀야 합니다.

### v1: 친구 1명과 상태 공유

```text
- SignalR server
- hard-coded test users
- UpdatePresence
- FriendPresenceChanged
- 친구 pet 1개 표시
- offline TTL
```

### v2: 친구 여러 명

```text
- friend list
- accepted friendship only
- 여러 pet 배치
- pet별 위치 저장
- 상태별 말풍선
```

### v3: 프라이버시/정확도 개선

```text
- 앱 제외 목록
- 상태 공유 pause
- idle detection
- audio session detection
- browser extension은 선택 기능으로만
```

### v4: 배포

```text
- installer
- auto start 옵션
- auto update
- crash log
- settings export/import
```

---

## 10. Codex에 줄 작업 지시문

Codex CLI를 쓸 경우 공식 문서 기준으로 `npm i -g @openai/codex`로 설치하고 `codex`로 실행할 수 있으며, Codex CLI는 선택한 디렉터리에서 코드를 읽고 수정하고 명령을 실행할 수 있습니다. ([OpenAI 개발자][7])

처음에 repo 루트에 이런 지시를 넣고 시작하는 게 좋습니다.

```text
You are building a Windows-only desktop presence pet app.

Hard constraints:
- Do not use keylogging.
- Do not capture screen contents.
- Do not read document body text.
- Do not scrape browser history.
- Do not send raw process names or window titles to the server by default.
- The overlay must not steal focus.
- The overlay must be click-through in normal mode.
- The app must not inject code into other processes.
- The app only reads foreground window metadata using Win32 APIs.

Architecture:
- WPF desktop app.
- ASP.NET Core SignalR server.
- Shared DTO project.
- Rule-based activity classifier.
- Sprite-based pet renderer.
- Privacy-first settings.

Build incrementally with tests for the classifier.
```

### Codex 작업 1: 프로젝트 생성

```text
Create a .NET solution named PetPresence.

Projects:
1. PetPresence.Desktop: WPF desktop app.
2. PetPresence.Server: ASP.NET Core minimal API + SignalR.
3. PetPresence.Contracts: shared DTOs and enums.

Add project references:
- Desktop references Contracts.
- Server references Contracts.

Do not implement networking yet.
Create a basic README with architecture and privacy constraints.
```

### Codex 작업 2: foreground activity detector

```text
In PetPresence.Desktop, implement Activity/ForegroundWindowReader.cs.

Requirements:
- Use P/Invoke for GetForegroundWindow, GetWindowThreadProcessId, GetWindowText.
- Return process id, process name, window title, captured timestamp.
- Ignore the current app's own process.
- Handle exceptions safely.
- Add an interface IForegroundWindowReader for testability.
```

### Codex 작업 3: classifier

```text
Implement Activity/ActivityClassifier.cs.

Input:
- ForegroundAppSnapshot
- idle seconds

Output:
- ActivityState with Kind, StatusText, AnimationKey, Confidence.

Rules:
- idle > 300 seconds => Away.
- Word/HWP/Notion/Obsidian/Typora => WritingDocument.
- VS Code/Visual Studio/PyCharm/Rider => Coding.
- Spotify/AppleMusic/MusicBee/foobar2000 => ListeningMusic.
- Browser + title containing YouTube/Netflix/Twitch/Disney/Tving/Wavve => WatchingVideo.
- Browser + search-related title => WebBrowsing.
- Browser otherwise => WebBrowsing.
- fallback => Unknown.

Add unit tests for common process/title combinations.
```

### Codex 작업 4: 오버레이

```text
Implement OverlayWindow in WPF.

Requirements:
- Transparent background.
- WindowStyle=None.
- AllowsTransparency=True.
- Topmost=True.
- ShowInTaskbar=False.
- Does not steal focus.
- In normal mode, mouse clicks pass through to underlying windows.
- In edit mode, click-through is disabled and pet controls can be dragged.
- Render multiple FriendPetViewModel items on a Canvas.
- Each pet has DisplayName, StatusText, AnimationKey, X, Y.
```

### Codex 작업 5: SignalR 서버

```text
Implement PresenceHub in PetPresence.Server.

For MVP:
- Use simple development auth via header X-User-Id.
- OnConnected, add connection to group user:{userId}.
- Implement UpdatePresence(PresenceUpdateDto update).
- For now, broadcast to all connected clients except sender.
- Later we will restrict to accepted friends.

Also implement:
- Presence TTL cleanup.
- Offline event when connection closes.
```

### Codex 작업 6: 친구 권한

```text
Add friendship model and authorization.

Requirements:
- Only accepted friends receive presence updates.
- Do not expose raw process names or window titles.
- Add endpoints:
  POST /friends/request
  POST /friends/accept
  POST /friends/block
  GET /friends
- PresenceHub should send updates only to accepted friends.
```

---

## 11. 핵심 리스크와 해결책

### 리스크 1: 상태가 너무 자주 바뀜

해결:

```text
- same state 3~5초 유지 후 확정
- 상태 전송 최소 간격 10초
- heartbeat 30초
- UI 말풍선 최소 표시 시간 5초
```

### 리스크 2: 브라우저 안에서 뭘 하는지 부정확함

해결:

```text
MVP:
  window title 기반 추정

후속:
  선택형 브라우저 확장
  단, URL 원문 서버 전송 금지
  로컬에서 category만 산출
```

### 리스크 3: 오버레이가 다른 앱을 방해함

해결:

```text
- 기본 click-through
- no activate
- taskbar 미표시
- tray에서만 설정 접근
- edit mode에서만 마우스 이벤트 수신
```

### 리스크 4: 프라이버시 우려

해결:

```text
- 친구 승인제
- 원본 process/window title 서버 전송 금지
- 앱별 제외 목록
- 상태 공유 pause
- "대략적 상태만 공유" 모드
- 로컬 로그에도 민감 정보 저장 금지
```

---

## 결론

가장 현실적인 MVP는 다음입니다.

```text
C# WPF Desktop
+ Win32 foreground window detection
+ rule-based activity classifier
+ transparent click-through pet overlay
+ ASP.NET Core SignalR presence server
+ privacy-first friend sharing
```

처음부터 “완벽히 현재 무엇을 하는지”를 맞히려 하지 말고, **foreground process + window title + idle time**으로 대략적 상태를 안정적으로 분류하세요. 이 앱의 품질은 AI보다 **오버레이가 방해되지 않는지, 상태가 과하게 노출되지 않는지, 친구에게 보이는 정보가 적절히 추상화되는지**에서 갈립니다.

[1]: https://developers.openai.com/codex/app/settings "Settings – Codex app | OpenAI Developers"
[2]: https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client?view=aspnetcore-10.0 "ASP.NET Core SignalR .NET Client | Microsoft Learn"
[3]: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow "GetForegroundWindow function (winuser.h) - Win32 apps | Microsoft Learn"
[4]: https://learn.microsoft.com/en-us/windows/win32/coreaudio/wasapi?utm_source=chatgpt.com "About WASAPI - Win32 apps"
[5]: https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency?view=windowsdesktop-10.0 "Window.AllowsTransparency Property (System.Windows) | Microsoft Learn"
[6]: https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features "Window Features - Win32 apps | Microsoft Learn"
[7]: https://developers.openai.com/codex/cli "CLI – Codex | OpenAI Developers"
