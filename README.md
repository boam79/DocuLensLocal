# DocuLens Local

Windows와 macOS에서 쓰는 설치형 PDF 검색기입니다. 계약서·MOU 같은 PDF를 **원래 폴더에 그대로 둔 채** 인덱싱하고, 파일명·본문·날짜로 찾습니다. 문서 원문은 외부 서버로 보내지 않습니다.

- 대상: Windows 10/11 64비트, macOS 12+(Apple Silicon·Intel)
- 스택: .NET 10, Avalonia, 로컬 SQLite
- 저장소: https://github.com/boam79/DocuLensLocal

## 설치 파일 받기

Windows 설치 파일 직접 주소:

- https://github.com/boam79/DocuLensLocal/releases/download/v0.1.5/DocuLensLocal-win-Setup.exe

GitHub Releases 페이지의 **Assets**가 잠시 돌아가 보일 수 있습니다. 스피너가 끝나지 않으면 위 주소를 브라우저에 붙여 넣으세요.

- 릴리스 목록: https://github.com/boam79/DocuLensLocal/releases
- 최신 릴리스: https://github.com/boam79/DocuLensLocal/releases/latest
- Windows 파일 이름: `DocuLensLocal-win-Setup.exe`

받은 파일을 실행하면 설치 화면(진행 표시)이 나옵니다. 코드 서명이 없어 Windows가 경고를 띄울 수 있습니다. 추가 정보를 연 뒤 실행을 선택하세요.

설치가 끝나면 **인덱싱할 폴더를 선택하세요** 창이 뜹니다. 빈 화면이 아니라 이 안내가 보여야 설치가 끝난 것입니다.

다른 컴퓨터에 쓰려면 그 컴퓨터에도 이 설치 파일을 받아 설치하고, 그 PC에서 열 수 있는 PDF 폴더를 선택하세요. 폴더 이름이 `계약서`가 아니어도 됩니다.

macOS 설치 패키지는 아직 GitHub Releases에 없습니다. Mac에서는 아래 **개발용으로 실행**으로 프로그램을 열고 테스트하세요.

## 최초 실행

등록된 폴더가 없으면 다음 안내가 나옵니다.

- 제목: 인덱싱할 폴더를 선택하세요
- 본문: 검색할 PDF가 들어 있는 폴더를 선택하세요. 이 컴퓨터의 폴더 이름은 ‘계약서’가 아니어도 됩니다.
- 보조: 다른 컴퓨터에서는 폴더 이름과 위치가 다를 수 있습니다. 그 컴퓨터에 있는 PDF 폴더를 새로 선택하면 됩니다.

글자가 들어 있는 PDF는 OCR하지 않고 기존 텍스트만 읽습니다. 스캔(이미지) 페이지만 백그라운드에서 OCR합니다. 사용자는 페이지가 하나씩 뜨는 화면을 보지 않습니다.

## 개발용으로 실행

필요 환경: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows 또는 macOS)

macOS Homebrew 예:

```bash
brew install --cask dotnet-sdk
```

설치 확인:

```bash
dotnet --version
```

`10.`으로 시작하는 버전이면 됩니다.

```bash
git clone https://github.com/boam79/DocuLensLocal.git
cd DocuLensLocal
dotnet test
dotnet build
dotnet run --project src/DocuLensLocal.App
```

Windows PowerShell에서도 같은 `dotnet` 명령을 씁니다.

솔루션 파일은 `DocuLensLocal.slnx` 입니다.

| 프로젝트 | 역할 |
|---|---|
| `src/DocuLensLocal.App` | Avalonia 화면 (Windows·macOS) |
| `src/DocuLensLocal.Worker` | 인덱싱·OCR 백그라운드 |
| `src/DocuLensLocal.Core` | 공통 로직 |
| `tests/DocuLensLocal.Core.Tests` | 테스트 |

검색 인덱스와 설정은 OS 로컬 앱 데이터에 둡니다. 원본 PDF는 읽기만 합니다.

- Windows: `%LOCALAPPDATA%\DocuLensLocal\userdata`
- macOS: `~/Library/Application Support/DocuLensLocal/userdata`

설치 파일을 이 저장소에서 다시 만들려면:

Windows (PowerShell):

```powershell
./scripts/pack.ps1
```

macOS / Linux:

```bash
./scripts/pack.sh
```

Windows 결과는 `artifacts/Releases/DocuLensLocal-win-Setup.exe` 입니다. macOS 설치 패키지는 Mac에서 `assets/app.icns`가 있을 때 만들어집니다. 아이콘이 없으면 publish만 하고, 개발 테스트는 `dotnet run`을 쓰세요.
