# DocuLens Local — Scratchpad

## Background and Motivation

사용자 요청: 폴더 선택 후 **인덱싱**이 가능해야 한다. 원본 PDF는 읽기만 하고, 인덱스는 Velopack `current\`가 아닌 `%LOCALAPPDATA%\DocuLensLocal\userdata`에 둔다.

이번 Executor 슬라이스: **Core 인덱싱 API만**. WPF(인덱싱 버튼)는 다른 에이전트가 담당한다. PDFium 본문 추출·OCR은 stub. 파일명/경로·크기·mtime이 로컬 저장소에 남고, UI가 진행률을 받을 수 있으면 성공.

## Key Challenges and Analysis

- 인덱스를 `AppPaths.UserData`에 두어야 업데이트 시 데이터가 지워지지 않는다.
- 원본 PDF를 수정하면 안 된다 (FileAccess.Read, 타임스탬프 유지).
- UI는 `IProgress`/`Start` + `CancellationToken`이면 충분하다.
- 테스트에서 실제 `%LOCALAPPDATA%`를 건드리지 않도록 UserData 경로를 주입한다.

## High-level Task Breakdown

### Task A — Core IndexingService (이번 슬라이스)

- 성공 기준: `dotnet test` 통과. UI가 `new IndexingService().Start(folder, progress, ct)` 호출 가능.
- `*.pdf` 재귀·대소문자 무시. SQLite에 path/size/mtime 기록. 진행률 보고. 추출/OCR TODO stub.

## Project Status Board

- [x] 솔루션 골격·폴더 선택 설정 경로
- [ ] Core IndexingService — 구현·`dotnet test` 12/12 통과. 사용자 확인 대기
- [ ] WPF 인덱싱 버튼 — UI가 `IndexingService.Start`를 호출함. 사용자 확인 대기 (완료 표시 금지)
- [x] v0.1.2 패키징·GitHub Releases 업로드 (인덱싱 버튼 포함). Planner 완료 표시는 사용자 확인 후
- [ ] 인덱싱 완료 후 메인 검색 화면 — 구현됨. 사용자 수동 확인 대기 (완료 표시 금지)
- [x] v0.1.3 패키징·GitHub Releases 업로드 (인덱싱 완료 후 검색 화면). Planner 완료 표시는 사용자 확인 후
- [ ] 검색 UI 현대화 + 정보 탭(버전/히스토리/업데이트) — 구현됨. 사용자 수동 확인 대기 (완료 표시 금지)
- [x] v0.1.4 패키징·GitHub Releases 업로드 (정보 탭 목업 3 스타일). Planner 완료 표시는 사용자 확인 후
- [ ] 자연어 파일명 검색(버스 광고 찾아줘) — 구현·테스트 통과. 사용자 확인 대기 (완료 표시 금지)
- [ ] 본문 추출·OCR·근거 검색 — 구현·테스트 45 통과. 사용자 Mac/`dotnet run` 확인 대기 (완료 표시 금지)
- [ ] macOS 개발·실행 — Avalonia `net10.0`, Mac 설치 없음. `dotnet run`으로 테스트. 사용자 확인 대기 (완료 표시 금지)

## Executor's Feedback or Assistance Requests

- 2026-08-18: [Core PDF 인덱싱](eb37c726-b43d-42f2-91bc-ac502f16e118) — `IndexingService` 구현 완료. WPF는 수정하지 않음. 사용자 확인 대기.
- UI 호출: `var svc = new IndexingService(); await svc.Start(folderPath, progress, ct);`
- 인덱스 파일: `AppPaths.IndexDatabase` = `%LOCALAPPDATA%\DocuLensLocal\userdata\index.db`
- PDFium/OCR는 TODO stub. 파일명·경로·크기·mtime은 저장되고 `Status = "indexed"`.
- 2026-08-18 (WPF): [WPF 인덱싱 버튼](e2cc63d9-6729-47b8-b1ef-2d47d4f0b6df)이 `MainWindow`에 **인덱싱**을 연결함. 폴더 선택만으로는 시작하지 않음. Core `IndexingService.Start` 호출 확인됨. 커밋하지 않음.
- Planner/사용자에게 수동 확인 요청: 폴더 없는 상태에서 버튼이 꺼져 있는지, 폴더 선택 후 **인덱싱**을 눌러 건수·현재 파일이 보이는지. 확인되면 이 두 항목을 완료로 표시해 주세요.
- 2026-08-18 (패키징 Executor): **v0.1.2** 팩·업로드 완료. Setup.exe를 `-Wait`로 실행하지 않음.
  - 로컬: `C:\Users\tttt\DocuLensLocal\artifacts\Releases\DocuLensLocal-win-Setup.exe`
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.2/DocuLensLocal-win-Setup.exe (HEAD 200)
  - 0.1.1 대비: splash + 폴더 선택에 **인덱싱 버튼 + Core IndexingService** 추가. 설치 페이로드만 업로드(PDF/인덱스 없음).
  - 버전 bump 커밋 `a738e66` push됨. Planner에게 설치본 수동 확인 요청.
- 2026-08-18 (검색 화면 Executor): 인덱싱 `Start` 성공 후 최초실행 폴더 UI를 닫고 **문서 검색** 화면으로 전환. 설치본 v0.1.2에는 없음. 커밋·팩·업로드 하지 않음.
  - 시작 시 `index.db`에 문서가 있거나 `settings.IndexCompleted`이면 검색 화면. 폴더 미선택이면 기존 PRD 최초실행 문구 유지.
  - 0건 인덱싱도 검색 화면(빈 상태). 폴더 선택만으로는 인덱싱 시작 안 함. 원본 PDF 수정 없음.
  - `dotnet test` 18/18, App 빌드 성공.
  - 수동 확인용 exe: `src\DocuLensLocal.App\bin\Debug\net10.0-windows\DocuLensLocal.exe` (설치된 0.1.2를 닫고 실행).
  - Planner/사용자에게 확인 요청: 인덱싱 완료 시 자동으로 검색 화면인지, 재실행 시 검색 화면인지, 폴더 없는 최초실행은 그대로인지.
- 2026-08-18 (릴리스 Executor): **v0.1.3 uploaded.** Setup.exe를 `-Wait`로 실행하지 않음.
  - 커밋 `27285cf` push됨: Open filename search after indexing so first-run is not the end, and bump installer to 0.1.3.
  - 로컬: `C:\Users\tttt\DocuLensLocal\artifacts\Releases\DocuLensLocal-win-Setup.exe`
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.3/DocuLensLocal-win-Setup.exe (HEAD 200)
  - 0.1.2 대비: 인덱싱 완료 후 최초실행에 머물지 않고 **파일명 검색 메인 화면**으로 전환. 설치 페이로드만 업로드(PDF/인덱스 없음).
  - Planner에게 설치본 수동 확인 요청.
- 2026-08-18 (UI Executor): 검색 화면을 라이트 모던(둥근 카드·틸 액센트·상단 탭)으로 다듬고 **정보** 탭을 추가함. 커밋·팩·업로드하지 않음.
  - 목업 3장 생성: 라이트 모던 / 사이드바형 / 정보 탭. 구현은 라이트 모던(상단 검색|정보) 방향.
  - 정보 탭: 제품명 + 어셈블리 버전(현재 0.1.3), 하드코드 히스토리 0.1.0–0.1.3, **업데이트** 버튼(Velopack `GithubSource` + `UpdateManager`). 없으면 「최신 버전입니다.」. Debug exe는 설치본이 아니라 적용하지 않음(성공으로 위장하지 않음).
  - 동작 유지: 폴더 선택만으로 인덱싱 안 함, 원본 PDF 읽기 전용, `userdata\index.db`, 파일명 검색.
  - `dotnet test` 27/27, App 빌드 성공.
  - 수동 확인용 exe: `src\DocuLensLocal.App\bin\Debug\net10.0-windows\DocuLensLocal.exe`
  - Planner/사용자에게 확인 요청: 검색·정보 탭, 버전 표시, 업데이트 버튼 메시지, 더 손볼 UI 방향(라이트/사이드바).
- 2026-08-18 (버전 bump Executor): 설치/앱 버전 선언을 **0.1.3 → 0.1.4**로 올리고 이력에 「정보 탭·모던 UI」를 맨 앞에 추가함. 커밋·팩·업로드하지 않음.
- 2026-08-18 (정보 탭 스타일 Executor): 목업 이미지 3에 맞춰 정보 탭만 다시 스타일함. 버전 선언·ReleaseHistory는 수정하지 않음. 커밋·팩하지 않음.
  - 버전 뱃지: 민트칩+진한 글자 → 틸 필 알약 + 흰 글자(어셈블리 버전).
  - 히스토리: 작은 점 목록 → 세로 타임라인(최신 실원, 이전 빈원, 연결선, 회색 버전 칩).
  - 업데이트: 왼쪽 작은 버튼 → 카드 전체 너비 틸 버튼. 캡션 가운데.
  - 상단 탭을 왼쪽 정렬. 검색 탭 동작(폴더/검색/다시 인덱싱) 유지, 같은 라이트 카드·틸 크롬.
  - App 빌드 성공, `dotnet test` 27/27.
  - 수동 확인: `src\DocuLensLocal.App\bin\Debug\net10.0-windows\DocuLensLocal.exe` → **정보** 탭이 목업과 가까운지.
- 2026-08-18 (릴리스 Executor): **v0.1.4 uploaded.** Setup.exe를 `-Wait`로 실행하지 않음.
  - 커밋 `970672a` push됨: Restyle the Info tab to match design mockup 3 and bump the installer to 0.1.4.
  - 로컬: `C:\Users\tttt\DocuLensLocal\artifacts\Releases\DocuLensLocal-win-Setup.exe`
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.4/DocuLensLocal-win-Setup.exe (HEAD 200)
  - 0.1.3 대비: 정보 탭 타임라인 + 틸 버전 알약(흰 글자) + 전체너비 업데이트 버튼 / 모던 크롬. 설치 페이로드만 업로드(PDF/인덱스 없음).
  - Planner에게 설치본 수동 확인 요청.
- 2026-08-18 (검색 버그 Executor): PRD P0/P1 스토리를 코드·index.db·테스트로 실행. 화면 버그 「버스 광고 찾아줘」→ 조건에 맞는 파일이 없습니다 를 수정함. 커밋·팩은 이어서 0.1.5로 진행.
  - 원인: `SearchByFileName`이 path LIKE 전체 문장만 봄. 「찾아줘」가 파일명에 없어서 0건.
  - 실제 index.db(읽기 전용): 276건. `버스`/`광고` 파일명 13건(예: `버스티브이.pdf`, `…광고 계약서.pdf`). 둘 다 들어 있는 파일명은 없음 → AND 0건이면 OR로 보여야 함.
  - 수정: 공백 분리 + 찾아줘/해줘/관련/문서 등 제거. 의미 토큰 AND 우선, 비면 OR. 본문은 PC 밖으로 안 보냄. MainWindow는 기존 `SearchByFileName` + 빈 검색=전체.
  - 실행한 스토리: 최초실행 폴더 문구(XAML PRD 문구 일치), 인덱싱 원본 비변경(기존 테스트), 파일명 키워드(mou 테스트 유지), NL 파일명 검색(신규 테스트), 정보 탭 버전(0.1.5).
  - 남은 P0: PDFium 본문 추출·OCR·미리보기·근거 문장. 본문 검색은 stub. 파일명 NL만 이번 슬라이스.
  - `dotnet test` 34/34, App 빌드 성공.
  - 수동 확인: Debug `src\DocuLensLocal.App\bin\Debug\net10.0-windows\DocuLensLocal.exe` 검색창에 `버스 광고 찾아줘`. 설치본은 0.1.5 업데이트 후.
- 2026-08-18 (릴리스 Executor): **v0.1.5 uploaded.** Setup.exe를 `-Wait`로 실행하지 않음.
  - 커밋 `77472ef` push됨: Match Korean NL filename queries and bump installer to 0.1.5.
  - 로컬: `C:\Users\tttt\DocuLensLocal\artifacts\Releases\DocuLensLocal-win-Setup.exe`
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.5/DocuLensLocal-win-Setup.exe (HEAD 302, follow 200, 72880377 bytes)
  - 0.1.4 대비: 「버스 광고 찾아줘」가 파일명 키워드(버스/광고)로 검색됨. 설치 페이로드만 업로드(PDF/인덱스 없음).
  - Planner에게 설치본 수동 확인 요청. 완료 표시는 사용자 확인 후.
- 2026-08-19 (macOS Executor): WPF(`net10.0-windows`)를 Avalonia 11.3.20(`net10.0`)으로 바꿔 Windows·Mac에서 같은 앱을 빌드·실행하게 함.
  - `dotnet test` 37/37 (Linux 클라우드). `dotnet build` App 0 warning.
  - 인덱스 경로: Windows `%LOCALAPPDATA%\DocuLensLocal`, Mac `~/Library/Application Support/DocuLensLocal`.
  - pack.ps1은 PATH의 `dotnet` + OS별 RID(`win-x64`/`osx-arm64`/`osx-x64`). `scripts/pack.sh` 추가.
  - Mac 설치 pkg는 `assets/app.icns` + 실제 Mac이 필요. 개발 테스트는 `dotnet run --project src/DocuLensLocal.App`.
  - 버전 선언 0.1.6. GitHub Releases 업로드는 하지 않음.
  - Planner/사용자에게 확인 요청: Mac에서 SDK 10 설치 후 `dotnet test`와 앱 실행, 폴더 선택·인덱싱·검색.
- 2026-08-19 (사용자 정정): Mac 설치(.pkg)는 하지 않는다. Mac은 개발·실제 앱 테스트만. pack.sh·osx RID 패키징 제거. `pack.ps1`은 Windows Setup.exe만. README도 설치 대상은 Windows, Mac은 `dotnet run`.
- 2026-08-19 (본문·OCR Executor): 파일명만 저장하던 인덱스를 **본문+OCR**로 바꿈.
  - PdfPig 디지털 텍스트. 글자 거의 없고 이미지가 있으면 Tesseract CLI(kor+eng) OCR. 원문은 서버로 안 보냄, 원본 mtime 유지.
  - 검색: 파일명 또는 본문. 결과 칩(파일명/본문/OCR) + 근거 스니펫. 더블클릭으로 PDF 열기.
  - `dotnet test` 45/45.
  - 사용자 확인: 본문에만 있는 단어로 검색되는지, 스캔 PDF는 tesseract 설치 후인지.

- 2026-08-20 (릴리스 Executor): **v0.1.7 uploaded.** Setup.exe를 `-Wait`로 실행하지 않음.
  - main `daa2a33` + tag `v0.1.7` push.
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.7/DocuLensLocal-win-Setup.exe
  - 0.1.5 대비: Avalonia(Mac에서 `dotnet run`), 본문 추출, 로컬 OCR, 근거 문장. 설치 페이로드만 업로드.
  - Windows 설치본은 정보 탭 **업데이트**로 0.1.7을 받을 수 있음.

- 2026-08-20 (UI 점검): 화면에서 276건은 보이는데 검색 버튼·목록·하단 버튼 글자가 없음.
  - 원인: Avalonia `ContentPresenter`에 `Content="{TemplateBinding Content}"`가 없음.
  - 수정: 0.1.8. 인덱스는 살아 있음. 업데이트 후 파일명/본문이 목록에 보여야 함.
- 2026-08-20 (0.1.11 Executor): OCR 속도. 페이지마다 TesseractEngine 생성 + kor+eng 동시 로드가 병목.
  - 엔진 재사용, 한국어 우선(글자 적으면 영어 한 번), 회색조 120dpi JPEG, PDF 스트림 재사용, 본문 있는 파일은 건너뜀, 빈 페이지에 CLI 재실행 안 함.
  - `dotnet test` 66/66. 사용자: 정보 탭 업데이트 후 다시 인덱싱 체감 속도 확인.
- 2026-08-20 (0.1.13 Executor): 검색 초기 화면이 너무 비어 보임. 안내 문구·예시 칩(버스 광고/부대/계약)·폴더 경로 표시. 파일 목록은 검색 후에만.

## Current Status / Progress Tracking

- 모드: **Executor** (v0.1.12 검색 초기 화면 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- 화면 증상(사용자 스크린샷): `인덱싱 완료 · 276건 · 본문 0건 · OCR 엔진 없음`, `부대` 검색은 파일명만 봄.
- 원인: 예전 파일명-only `index.db`를 그대로 씀. Windows에 Tesseract가 PATH에 없어 OCR 배지까지 꺼짐.
- 이번 슬라이스: 설치본에 Tesseract native + tessdata(kor/eng) 포함. 본문 0건이면 저장된 폴더를 자동 재인덱스. 버전 0.1.10.
- `dotnet test` 63/63 (Linux 클라우드).
- **v0.1.10 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.10/DocuLensLocal-win-Setup.exe
- Planner/사용자: 정보 탭 **업데이트** 후 앱을 다시 켜면 본문을 자동으로 읽는지, `부대` 검색이 되는지 확인 요청. 완료 표시는 사용자 확인 후.

---

## Background and Motivation (2026-08-19 추가)

사용자 요청: Windows에서 시작한 프로젝트를 **Mac에서도 개발·프로그램 테스트**할 수 있게 한다. 저장소: https://github.com/boam79/DocuLensLocal.git

원인: `DocuLensLocal.App`이 `net10.0-windows` + WPF라 macOS/Linux에서 빌드·실행이 불가하다. `scripts/pack.ps1`도 `C:\Program Files\dotnet\dotnet.exe`에 고정되어 있다.

## Key Challenges and Analysis (2026-08-19 추가)

- WPF는 Windows 전용. Mac에서 GUI를 돌리려면 Avalonia 같은 크로스플랫폼 UI가 필요하다.
- Core/Worker/Tests는 이미 `net10.0`이라 경로만 OS API(`LocalApplicationData`)를 쓰면 Mac·Linux에서도 동작한다.
- Velopack Mac 패키징은 **Mac에서만** 가능. 개발·테스트는 `dotnet run`으로 충분하다.
- 인덱스는 Windows `%LOCALAPPDATA%\DocuLensLocal`, Mac `~/Library/Application Support/DocuLensLocal`.

## High-level Task Breakdown (2026-08-19)

## Background and Motivation (2026-08-19 본문·OCR)

사용자: 프로젝트가 부족하다. 인덱싱/OCR이 제대로인지 모르겠고, 파일 검색기와 차이가 없다.

원인: 인덱스가 경로·크기·mtime만 저장. 본문 추출·OCR은 stub. 검색은 파일명 LIKE뿐.

차별점(이번에 구현):
- 디지털 PDF 본문을 이 PC에서만 추출 (PdfPig)
- 글자 없는 스캔 페이지는 로컬 Tesseract OCR
- 파일명+본문 검색, 히트 시 근거 문장(스니펫)
- 원문은 서버로 안 보냄, 원본 PDF 수정 없음

## High-level Task Breakdown (2026-08-19 본문·OCR)

### Task C — 본문 인덱스·OCR·근거 검색

- 성공 기준: 본문에만 있는 단어로 검색됨. 스니펫 표시. OCR 엔진이 있으면 이미지 페이지 글자를 넣음. `dotnet test` 통과. 원본 mtime 유지.

- 성공 기준: `dotnet test` 통과. App이 `net10.0`(WPF 아님). Mac에서 `dotnet run`으로 GUI 테스트. Windows 설치 팩만 `pack.ps1`. Mac 설치 파일은 만들지 않음.

## Lessons

- 기능 검증은 배포 URL에서 수행 (웹). 이 작업은 로컬 .NET 라이브러리라 `dotnet test`가 검증이다.
- `dotnet` PATH가 비면 `C:\Program Files\dotnet\dotnet.exe`를 쓴다.
- 인덱스를 Velopack `current\`에 두면 업데이트 때 사라진다. UserData만 사용.
- Microsoft.Data.Sqlite 기본 연결 풀은 Windows에서 `index.db`를 잠근다. `Pooling=false` + `ClearAllPools()`가 필요하다.
- 인덱싱 `완료`는 최초실행 화면의 종착점이 아니다. `Start` 성공(`IsCompleted`)이면 검색 화면으로 넘겨야 한다. 0건도 동일. 재시작은 `index.db` 문서 수 또는 `AppSettings.IndexCompleted`로 판단한다.
- Velopack `UpdateManager.IsInstalled`가 false인 Debug exe에서는 업데이트를 적용하지 말고, 「최신 버전입니다.」로 위장하지 않는다. 공개 저장소 확인은 `GithubSource(repoUrl, accessToken: null, prerelease: false)`.
- 한국어 NL 파일명 검색은 문장 전체 LIKE가 아니라 토큰(버스/광고) AND→OR. 실제 276건 인덱스에는 버스+광고 동시 파일명이 없어 OR 폴백이 필수다.
- WPF(`net10.0-windows`)는 Mac에서 빌드·실행이 안 된다. 데스크톱 UI는 Avalonia + `net10.0`으로 둔다. Mac 경로는 `LocalApplicationData` → `~/Library/Application Support/DocuLensLocal`.
- 사용자는 Mac 설치본을 원하지 않는다. Mac은 `dotnet run`으로 개발·기능 테스트만. `pack.ps1`은 Windows Setup.exe만 만든다.
- 파일명만 인덱싱하면 탐색기와 차별이 없다. 디지털 PDF는 PdfPig 본문, 스캔은 로컬 Tesseract. 검색 결과는 근거 스니펫이 있어야 한다.
- Windows 사용자는 Tesseract를 따로 설치하지 않는다. 설치본에 `x64/tesseract50.dll` + `tessdata/eng|kor.traineddata`(tessdata_fast)를 넣고, PATH CLI는 보조만 쓴다.
- 예전 인덱스(본문 0건)는 안내 문구만으로 해결되지 않는다. 저장된 `IndexFolder`가 있으면 시작 시 자동 재인덱싱해야 한다.
- charlesw Tesseract 5.2.0 native 파일 이름은 `tesseract41.dll`이 아니라 `tesseract50.dll`이다.
- OCR 속도: 페이지마다 `new TesseractEngine` 하지 말 것. `kor+eng` 동시 로드는 한국어만보다 훨씬 느리다. 회색조 120dpi + JPEG면 검색용으로 충분하다.
- 엑셀·HWP가 「인덱싱 안 됨」이면 먼저 검색 화면 오른쪽 아래 **실제 폴더 경로**를 본다. 파일은 `인수인계`에 있고 인덱스는 `인수인계\계약서_스캔`이면 상위 폴더 파일은 안 읽힌다. 하위만 재귀한다.
- 확장자 필터 버튼은 Idle 안내 안에 두면 검색 후 사라진다. 검색창 아래에 항상 두고, 이미 검색한 뒤에도 누르면 그 종류로 다시 찾게 한다.

## Background and Motivation (2026-08-20 Word·HWP)

사용자: 프로그램이 PDF만 본다. Word·HWP가 기술적으로 가능한지 확인하고, 가능하면 진행해 달라.

결론: 가능. 원본은 읽기만, 서버 업로드 없음, 인덱스는 userdata.

| 확장자 | 본문 추출 |
|---|---|
| `.docx` | ZIP + `w:t` XML |
| `.doc` | OLE `WordDocument` 스트림(OpenMcdf, 유니코드 FIB) |
| `.hwpx` | ZIP + `hp:t` XML |
| `.hwp` | HwpLibSharp `HWPReader` + `TextExtractor` |
| `.pdf` | 기존 PdfPig + OCR |

## High-level Task Breakdown (2026-08-20 Word·HWP)

### Task D — Word·한글 본문 인덱싱

- 성공 기준: 파일명이 달라도 DOCX/HWPX/HWP 본문 단어로 검색됨. txt/png는 제외. 원본 mtime 유지. UI 뱃지가 확장자를 말함. `dotnet test` 통과. 버전 0.1.14.

## Current Status / Progress Tracking

- 모드: **Executor** (Word·HWP 본문 검색 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- `dotnet test` 84/84. App 빌드 성공. 버전 0.1.14.
- 지원: PDF(기존 OCR) + DOCX/DOC + HWP/HWPX. txt/png/`~$` 잠금 파일은 제외. 원본 읽기 전용.
- 사용자 확인: 정보 탭 업데이트 후 **다시 인덱싱**, Word·한글 본문 단어로 검색되는지. 완료 표시는 사용자 확인 후.

## Executor's Feedback or Assistance Requests

- 2026-08-20 (0.1.14 Executor): Word·한글 본문 검색을 넣음. PDF만 보던 발견을 `.pdf/.docx/.doc/.hwp/.hwpx`로 넓힘. NPOI는 취약점 의존성 때문에 쓰지 않음. 예전 PDF 인덱스는 **다시 인덱싱**이 필요함.

## Background and Motivation (2026-08-20 인덱스 초기화)

사용자: 인덱싱한 것을 초기화하고 재인덱싱하는 기능도 넣어 달라.

지금 「폴더 변경 / 다시 인덱싱」은 폴더 선택 화면으로만 돌아간다. `index.db`는 그대로이고, 크기·mtime이 같으면 본문을 다시 읽지 않는다. 지워진 파일도 목록에 남을 수 있다.

## High-level Task Breakdown (2026-08-20 인덱스 초기화)

### Task E — 인덱스 비우고 처음부터 다시 읽기

- 성공 기준: Clear 후 검색 0건, 원본 파일 불변. Rebuild는 같은 파일이라도 추출기를 다시 돌림. Start는 폴더에 없는 경로를 목록에서 제거. 검색 화면에 「처음부터 다시 인덱싱」 버튼. `dotnet test` 통과. 버전 0.1.15.

## Current Status / Progress Tracking

- 모드: **Executor** (Word·HWP 스캔 OCR·업데이트 팝업 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- `dotnet test` 97/97. App 빌드 성공. 버전 0.1.16.
- **v0.1.16 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.16/DocuLensLocal-win-Setup.exe
- 글자가 거의 없는 Word/HWP는 큰 그림을 OCR. 글자가 많은 파일은 텍스트만(로고 OCR 생략).
- 시작 시 새 버전 팝업(확인/나중에). 확인 후 재시작되면 업데이트 내역 팝업.
- 예전에 인덱싱한 스캔 Word/HWP는 **처음부터 다시 인덱싱** 필요.
- 사용자 확인: 시작 팝업, 스캔 HWP/Word 검색, 원본 파일 그대로인지. 완료 표시는 사용자 확인 후.

## Background and Motivation (2026-08-20 검색 화면 파일 형식)

사용자: 검색 화면 가운데 「문서」 표시를 PDF, WORD, HWP로 바꿔, 어떤 파일이 가능한지 한눈에 알게 해 달라.

## High-level Task Breakdown (2026-08-20 검색 화면 파일 형식)

### Task G — 빈 검색 화면에 PDF·WORD·HWP

- 성공 기준: IdleHintPanel에 「문서」 한 칸 대신 PDF / WORD / HWP 세 칸. 안내 문구에 지원 형식이 보임. `dotnet test` 통과. 버전 0.1.17.

## Current Status / Progress Tracking (2026-08-20 추가 파일만 인덱싱)

- 모드: **Executor** (새 파일만 인덱싱 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- 검색 화면 **새 파일 인덱싱**: 이미 읽은 파일은 건너뛰고 새로 넣거나 바뀐 파일만 읽음.
- 앱을 켤 때 새 파일이 있으면 자동으로 같은 패스를 돌림.
- **처음부터 다시 인덱싱**은 전체 재읽기 그대로.
- `dotnet test` 112/112. 버전 0.1.19.
- **v0.1.19 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.19/DocuLensLocal-win-Setup.exe
- 사용자 확인: 폴더에 파일 추가 후 **새 파일 인덱싱** 또는 앱 재실행. 완료 표시는 사용자 확인 후.

## Current Status / Progress Tracking (2026-08-20 폴더 감시 자동 인덱싱)

- 모드: **Executor** (폴더에 넣으면 자동 인덱싱 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- 인덱싱이 끝난 폴더를 감시. PDF/Word/한글이 들어오면 약 3초 후 새 파일만 읽음.
- 검색 중이면 검색어를 지우지 않음. 최초 폴더 선택만으로는 감시하지 않음.
- `dotnet test` 116/116. 버전 0.1.20.
- **v0.1.20 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.20/DocuLensLocal-win-Setup.exe
- 사용자 확인: 앱을 켠 채 폴더에 파일 추가 후 자동으로 읽히는지. 완료 표시는 사용자 확인 후.

## Current Status / Progress Tracking (2026-08-20 업데이트 후 인덱싱 이어서)

- 모드: **Executor** (업데이트 후 인덱싱 이어서 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- 인덱싱 시작 시 `IndexingInProgress` 저장. 업데이트 확인 시 인덱싱을 멈추고, 다시 켜면 `Start`로 남은 파일부터 이어서 읽음(이미 본문이 있는 파일은 건너뜀).
- `dotnet test` 105/105. 버전 0.1.18.
- **v0.1.18 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.18/DocuLensLocal-win-Setup.exe
- 사용자 확인: 인덱싱 중 업데이트 → 다시 켠 뒤 남은 파일부터 이어지는지. 완료 표시는 사용자 확인 후.

## Current Status / Progress Tracking (2026-08-20 검색 화면 파일 형식)

- 모드: **Executor** (검색 화면 PDF·WORD·HWP — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- 가운데 「문서」 칸을 빨강 PDF / 파랑 WORD / 틸 HWP 세 칸으로 바꿈.
- 아래에 「PDF · Word · 한글(HWP) 파일을 찾을 수 있습니다」 안내.
- `dotnet test` 97/97. App 빌드 성공. 버전 0.1.17.
- **v0.1.17 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.17/DocuLensLocal-win-Setup.exe
- 사용자 확인: 검색 화면 가운데에 PDF·WORD·HWP가 보이는지. 완료 표시는 사용자 확인 후.

## Executor's Feedback or Assistance Requests

- 2026-08-20 (0.1.20 Executor): 인덱싱이 끝난 폴더를 감시. 파일을 넣으면 약 3초 후 **자동으로 새 파일만** 읽음. 설치본 v0.1.20 업로드. Setup.exe를 `-Wait`로 실행하지 않음.
- 2026-08-20 (0.1.19 Executor): **새 파일 인덱싱** — 이미 읽은 파일은 건너뛰고 신규·변경만 읽음. 앱 시작 시 새 파일이 있으면 자동. 설치본 v0.1.19 업로드. Setup.exe를 `-Wait`로 실행하지 않음.
- 2026-08-20 (0.1.18 Executor): 인덱싱 중 업데이트하면 다시 시작 후 **남은 파일부터 이어서** 읽음. 설치본 v0.1.18 업로드. Setup.exe를 `-Wait`로 실행하지 않음.
- 2026-08-20 (0.1.17 Executor): 검색 빈 화면 가운데 「문서」를 **PDF / WORD / HWP** 세 칸으로 바꿈. 설치본 v0.1.17 업로드. Setup.exe를 `-Wait`로 실행하지 않음.
- 2026-08-20 (0.1.15 Executor): 검색 화면에 **처음부터 다시 인덱싱**을 넣음. 검색창 **초기화**는 검색어만 지움(기존). 인덱스 초기화는 원본 문서를 건드리지 않음.

## Background and Motivation (2026-08-20 OCR·업데이트 팝업)

사용자: Word·HWP는 OCR이 안 되면 인덱싱만으로는 의미가 없다. 업데이트가 있으면 팝업 → 확인 시 적용 → 업데이트 내역 팝업.

글자가 들어 있는 Word/HWP는 이미 본문 검색이 된다. 스캔(그림만 있는) 파일은 그림 OCR이 필요하다. 업데이트는 정보 탭 버튼만 있고 확인 창이 없다.

## High-level Task Breakdown (2026-08-20 OCR·업데이트 팝업)

### Task F

- Word/HWP 글자가 거의 없으면 큰 그림을 OCR. 원본 수정 없음.
- 시작 시 새 버전 팝업(확인/나중에). 확인 시 적용. 재시작 후 내역 팝업.
- 버전 0.1.16. `dotnet test` 통과.

## Background and Motivation (2026-08-20 추가 파일만 인덱싱)

사용자: 인덱싱한 뒤에 폴더에 파일을 더 넣으면, 새로 넣은 파일만 인덱싱하게 해 달라. 매번 「처음부터 다시 인덱싱」은 할 수 없다.

`Start`는 이미 본문+크기+mtime이 같으면 건너뛰지만, 검색 화면에는 전체 재인덱싱 버튼만 있고, 앱을 다시 켜도 새 파일을 자동으로 읽지 않는다. 본문이 비어 있는 파일은 `Start`를 다시 하면 OCR을 또 돌린다.

## High-level Task Breakdown (2026-08-20 추가 파일만 인덱싱)

### Task I — 새 파일만 인덱싱

- 성공 기준: `IndexPass.NewAndChanged`는 새로 넣거나 바뀐 파일만 추출. 이미 목록에 있고 크기·mtime이 같으면 본문이 없어도 건너뜀. `PlanSync`가 신규/변경/삭제 건수를 알려 줌. 검색 화면 **새 파일 인덱싱** 버튼. 앱을 켤 때 새 파일이 있으면 자동으로 그 패스만 실행. Rebuild는 그대로. `dotnet test` 통과. 버전 0.1.19.

## Background and Motivation (2026-08-20 업데이트 후 인덱싱 이어서)

사용자: 인덱싱하는 과정 중에 업데이트하면, 업데이트가 끝난 뒤 인덱싱을 이어서 할 수 있게 해 달라.

지금은 인덱싱 중 Velopack이 프로세스를 다시 시작하면 `IndexingInProgress`를 기억하지 않는다. 본문이 일부라도 있으면 자동 백필도 돌지 않아, 남은 파일을 이어서 읽지 않는다.

## High-level Task Breakdown (2026-08-20 업데이트 후 인덱싱 이어서)

### Task H — 업데이트 후 남은 파일부터 이어서 인덱싱

- 성공 기준: 인덱싱 시작 시 설정의 `IndexingInProgress=true`. 중단 후 `Start`를 다시 호출하면 이미 본문이 있는 파일은 추출기를 건너뛴다. 앱이 다시 켜지면 폴더가 있을 때 자동으로 `Start`(Rebuild 아님). 업데이트 안내 문구에 「이어서」. `dotnet test` 통과. 버전 0.1.18.

## Background and Motivation (2026-08-20 폴더 감시 자동 인덱싱)

사용자: 추가 파일도 자동으로 인덱싱할 수 있게 해 달라. 버튼을 누르거나 앱을 다시 켜지 않아도 된다.

지금은 앱을 켤 때만 새 파일을 확인하고, 켜 둔 동안 폴더에 넣으면 읽지 않는다.

## High-level Task Breakdown (2026-08-20 폴더 감시 자동 인덱싱)

### Task J — 폴더에 파일이 들어오면 자동으로 읽기

- 성공 기준: 인덱싱이 끝난 폴더를 감시. PDF/Word/한글이 생기거나 바뀌거나 지워지면 잠깐 기다린 뒤 `NewAndChanged`만 실행. 검색 중이면 검색어를 지우지 않음. 폴더만 고른 최초실행은 감시하지 않음. `dotnet test` 통과. 버전 0.1.20.

## Background and Motivation (2026-08-20 Excel 인덱싱·OCR)

사용자: 엑셀이 인덱싱과 OCR이 안 되는 것 같다. 유저스토리에 입각해서 확인하고 수정해 달라.

유저스토리: 계약서·견적·MOU를 파일명만이 아니라 **본문·스캔 OCR**로 찾는다. 견적·계약이 `.xlsx`/`.xls`로 있는 경우가 많다. 기존 코드는 `IndexableFiles`에 Excel이 없어 폴더를 돌아도 건너뛰었다.

NPOI/ClosedXML은 쓰지 않는다(취약점·의존성). xlsx는 ZIP XML, xls는 OpenMcdf SST. 그림이 많고 글자가 거의 없으면 Word와 같은 OCR 임계값(80자)을 쓴다. `.xlsb`는 이번 범위 밖.

## High-level Task Breakdown (2026-08-20 Excel 인덱싱·OCR)

### Task K — Excel 본문 검색·스캔 OCR

- 성공 기준: `.xlsx`/`.xlsm`/`.xls`가 발견·인덱싱된다. 파일명이 달라도 셀 글자(견적·계약)로 검색된다. 글자가 거의 없고 큰 그림이 있으면 OCR. 원본 mtime 유지. `~$` 잠금 파일 제외. 빈 화면에 EXCEL 칸. `dotnet test` 통과. 버전 0.1.21.

## Current Status / Progress Tracking (2026-08-20 Excel 인덱싱·OCR)

- 모드: **Executor** (Excel 본문 검색·OCR — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- `dotnet test` 125/125. App 빌드 성공. 버전 0.1.21.
- **v0.1.21 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.21/DocuLensLocal-win-Setup.exe
- 지원 추가: `.xlsx` / `.xlsm` / `.xls`. 글자가 거의 없으면 `xl/media`·OLE 그림 OCR.
- 이미 인덱싱한 폴더의 Excel은 **새 파일 인덱싱**(또는 폴더 감시)으로 읽힘. 스캔 그림만 있는 파일은 **처음부터 다시 인덱싱**이 필요할 수 있음.
- 사용자 확인: 실제 `.xlsx` 견적/계약 본문 검색, 스캔 그림 Excel OCR. 완료 표시는 사용자 확인 후.

## Background and Motivation (2026-08-20 엑셀 추가 시 바로 인덱싱)

사용자: 엑셀 파일을 폴더에 넣으면 바로 새 파일 인덱싱이 되어야 하는데 안 되는 것 같다.

원인: Excel은 저장 시 확장자 없는 임시 파일/`~$` 잠금 파일을 쓴다. 감시 필터가 `*.*`라 임시 파일을 놓치고, `~$`는 무시했다. ZIP은 `FileShare.Read`로 열어서 Excel이 연 파일은 실패 → 빈 본문으로 저장 → 크기·mtime이 같으면 **새 파일 인덱싱**도 다시 안 읽었다.

## High-level Task Breakdown (2026-08-20 엑셀 추가 시 바로 인덱싱)

### Task L — 엑셀을 넣으면 바로 읽기

- 성공 기준: `~$`·`.tmp`·확장자 없는 임시 파일도 감시를 깨운다. xlsx는 Excel이 연 상태에서도 공유 읽기가 된다. 깨진 ZIP은 빈 본문으로 고정하지 않고 다음 패스에서 다시 읽는다. `dotnet test` 통과. 버전 0.1.22.

## Current Status / Progress Tracking (2026-08-20 엑셀 추가 시 바로 인덱싱)

- 모드: **Executor** (엑셀 넣으면 바로 인덱싱 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- `dotnet test` 127/127. 버전 0.1.22.
- **v0.1.22 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.22/DocuLensLocal-win-Setup.exe
- 감시 필터 `*`, `~$`/`.tmp`도 동기화 시작. ZIP은 `FileShare.ReadWrite`. 깨진 ZIP은 목록에 고정하지 않음.

## Executor's Feedback or Assistance Requests

- 2026-08-20 (Planner): 사용자 「확인완료」. 인덱싱 대상 폴더가 `계약서_스캔`이라 상위 `인수인계`의 xlsx/hwp는 범위 밖이었음.
- 2026-08-20 (0.1.22 Executor): 엑셀을 폴더에 넣어도 바로 안 읽히던 원인 — 임시/`~$` 감시 누락 + ZIP 배타 열기. 설치본 v0.1.22 업로드. Setup.exe를 `-Wait`로 실행하지 않음.

## Background and Motivation (2026-08-20 엑셀·HWP 인덱싱 재확인)

사용자: 인덱싱이 안 되니 다시 확인하고, HWP도 되는지 확인해 달라.

원인: 엑셀·한글을 한 번 빈 본문(`filename_only`)으로 저장하면 `NewAndChanged`가 크기·mtime이 같다고 건너뛴다. 앱을 업데이트해도, **새 파일 인덱싱**을 눌러도 다시 안 읽는다. HWP 추출은 예외를 삼켜 빈 본문으로 남겼다.

## High-level Task Breakdown (2026-08-20 엑셀·HWP 인덱싱 재확인)

### Task M — 빈 본문 엑셀·한글 다시 읽기

- 성공 기준: 본문이 비어 있는 xlsx/hwp는 증분 패스에서 다시 추출. 빈 PDF는 반복 OCR하지 않음. HWP/HWPX는 한글이 연 상태에서도 공유 읽기. `dotnet test` 통과. 버전 0.1.23.

## Current Status / Progress Tracking (2026-08-20 엑셀·HWP 인덱싱 재확인)

- 모드: **Executor** (엑셀·HWP 다시 읽기 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- `dotnet test` 131/131. 버전 0.1.23.
- **v0.1.23 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.23/DocuLensLocal-win-Setup.exe
- **Planner 2026-08-20:** 사용자 확인 완료. 엑셀·한글이 안 읽히던 직접 원인은 선택한 폴더가 `인수인계\계약서_스캔`이고, 파일은 상위 `인수인계`에 있었음. 폴더 변경으로 해결.


## Background and Motivation (2026-08-22 확장자별 검색)

사용자: 키워드 검색은 모든 종류가 섞여 나온다. 가운데 PDF / WORD / HWP / EXCEL 마크를 버튼처럼 눌러, 그 종류만 검색하고 싶다.

## High-level Task Breakdown (2026-08-22 확장자별 검색)

### Task N — 확장자 버튼으로 검색 필터

- 성공 기준: PDF→`.pdf`, WORD→`.docx`/`.doc`, HWP→`.hwp`/`.hwpx`, EXCEL→`.xlsx`/`.xlsm`/`.xls`. 다시 누르면 전체. 검색창 아래 버튼이 결과 화면에서도 보임. `dotnet test` 통과. 버전 0.1.24.

## Current Status / Progress Tracking (2026-08-22 확장자별 검색)

- 모드: **Executor** (확장자 필터 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- `dotnet test` 147 통과 (감시 타이밍 테스트 1건은 재실행 시 통과). 버전 0.1.24.
- **v0.1.24 uploaded.** GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.24/DocuLensLocal-win-Setup.exe
- PR: https://github.com/boam79/DocuLensLocal/pull/3

## Executor's Feedback or Assistance Requests

- 2026-08-22 (0.1.24 Executor): 검색창 아래 PDF/WORD/HWP/EXCEL을 눌러 그 종류만 검색. 같은 칸을 다시 누르면 전체. 설치본 v0.1.24 업로드. Setup.exe를 `-Wait`로 실행하지 않음. 사용자 수동 확인 요청.
- 2026-08-22 (확장자 검색 Executor): 가운데 형식 마크를 토글 버튼으로 바꿔, 선택한 확장자만 검색되게 구현 중.
- 2026-08-20 (0.1.21 Executor): Excel이 인덱싱 대상에 없어 견적·계약 엑셀을 건너뛰고 있었다. xlsx/xlsm ZIP 셀 글자 + xls SST, 글자 부족 시 그림 OCR. 설치본 v0.1.21 업로드. Setup.exe를 `-Wait`로 실행하지 않음.

## Background and Motivation (2026-08-22 고도화 제안)

사용자: 프런트 디자인과 기능 고도화 제안을 해 달라. 구현 지시 없음. **Planner**만.

제품 전제 유지: 원문은 PC 밖으로 안 보냄, 원본 읽기만, 폴더 하나 중심의 계약/인수인계 검색, 비기술 사용자, Mac 설치본 없음.

## Key Challenges and Analysis (2026-08-22 고도화 제안)

이미 되는 것: 파일명+본문+OCR, 확장자 필터, 자동 감시, 업데이트. 빈틈은 「찾기」보다 「찾고 나서 이해하고 열기」.

실제 사용에서 드러난 점:
- 폴더 경로가 화면 맨 아래 회색이라, 상위 폴더 파일을 못 찾는 원인을 사용자가 못 봄.
- 결과는 더블클릭으로만 열림. 열기 버튼이 없음.
- 근거 문장에 검색어가 굵게 안 들어감.
- 아래쪽 「폴더 변경 / 새 파일 인덱싱 / 처음부터 다시 인덱싱」이 비슷해 보임.
- 결과 종류 칸은 전부 같은 틸 색. 빈 화면 PDF(빨강)/WORD(파랑)과 불일치.
- 폴더는 하나뿐. 날짜·최근 검색·여러 종류 동시 선택은 없음.

하지 말 것: 문서를 서버/AI로 보내는 기능, 복잡한 설정 화면, Mac 설치본, 미리보기 OCR 뷰어(큼).

## High-level Task Breakdown (2026-08-22 고도화 제안) — 아직 착수 금지

우선순위는 사용자가 고른 뒤 Executor가 한 장씩.

### P0 — 검색 결과를 더 읽기 쉽게 (추천 다음 장)

- 근거 문장에서 검색어 강조, 한 번 클릭 **열기**, 종류 칸 색을 PDF/WORD/HWP/EXCEL과 맞춤, 긴 경로 대신 상위 폴더명, 「폴더에서 보기」.
- 성공 기준: 더블클릭 없이도 파일을 열고, 왜 맞았는지 한눈에 보임.

### P1 — 화면을 검색 도구처럼

- 아래 3버튼을 「폴더」 한 줄로 정리. 다시 인덱싱은 확인 후. 폴더 경로를 제목 근처로. 최초실행은 「이 폴더 읽기」 한 버튼. 인덱싱 진행 막대.

### P2 — 찾기 조건

- 종류 여러 개 동시(PDF+HWP). 날짜(올해/최근 1년). 최근 검색어. 결과 건수. Enter로 첫 결과 열기.

### P3 — 나중

- 여러 폴더, 페이지 번호, 트레이 아이콘, 다크 모드. 클라우드 검색/요약은 제품 약속과 충돌하므로 제외.

## Current Status / Progress Tracking (2026-08-22 고도화 제안)

- 모드: **Planner**. 구현·버전 bump 없음. 사용자 선택 대기.

