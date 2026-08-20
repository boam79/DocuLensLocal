# DocuLens Local

Windows 10/11용 **로컬 문서 검색기**입니다. 탐색기 파일명 검색과 달리, 계약서·MOU를 원래 폴더에 둔 채 **본문과 스캔 글자까지** 이 컴퓨터에서만 인덱싱합니다. 문서 원문은 외부 서버로 보내지 않습니다.

지원 파일: **PDF, Word(`.docx`/`.doc`), 한글(`.hwp`/`.hwpx`)**. txt·이미지 같은 다른 파일은 건너뜁니다.

개발은 Windows와 macOS 모두에서 `dotnet test` / `dotnet run`으로 합니다. Mac용 설치 파일은 만들지 않습니다.

- 설치 대상: Windows 10/11 64비트
- 스택: .NET 10, Avalonia, PdfPig, HwpLibSharp, 내장 Tesseract(kor/eng), 로컬 SQLite
- 저장소: https://github.com/boam79/DocuLensLocal

## 탐색기와 다른 점

| | 파일 탐색기 / Spotlight | DocuLens Local |
|---|---|---|
| 찾는 대상 | 주로 파일 이름 | 파일 이름 + PDF·Word·한글 본문 |
| 스캔(이미지) PDF | 거의 못 찾음 | 글자가 없으면 로컬 OCR |
| 검색 결과 | 경로만 | 본문 **근거 문장** |
| 원문 유출 | OS 인덱서에 따라 다름 | PC 밖으로 안 보냄 |
| 원본 파일 | — | 읽기만, 수정 없음 |

예: 파일명이 `내부문서.docx`여도 본문에 「버스 광고 계약」이 있으면 `버스 광고 찾아줘`로 찾습니다. PDF·HWP도 같습니다.

## 설치 파일 받기

설치 파일 직접 주소:

- https://github.com/boam79/DocuLensLocal/releases/download/v0.1.14/DocuLensLocal-win-Setup.exe

GitHub Releases 페이지의 **Assets**가 잠시 돌아가 보일 수 있습니다. 스피너가 끝나지 않으면 위 주소를 브라우저에 붙여 넣으세요.

- 릴리스 목록: https://github.com/boam79/DocuLensLocal/releases
- 최신 릴리스: https://github.com/boam79/DocuLensLocal/releases/latest
- 파일 이름: `DocuLensLocal-win-Setup.exe`

받은 파일을 실행하면 설치 화면(진행 표시)이 나옵니다. 코드 서명이 없어 Windows가 경고를 띄울 수 있습니다. 추가 정보를 연 뒤 실행을 선택하세요.

설치가 끝나면 **인덱싱할 폴더를 선택하세요** 창이 뜹니다. 빈 화면이 아니라 이 안내가 보여야 설치가 끝난 것입니다.

다른 컴퓨터에 쓰려면 그 컴퓨터에도 이 설치 파일을 받아 설치하고, 그 PC에서 열 수 있는 문서 폴더를 선택하세요. 폴더 이름이 `계약서`가 아니어도 됩니다.

Word·한글 본문 검색은 v0.1.14 설치본과 이 저장소의 `dotnet run`에 들어 있습니다. 예전에 PDF만 인덱싱했다면 **폴더 변경 / 다시 인덱싱**을 한 번 누르세요. 앱을 켜면 검색창과 예시 단어만 보입니다. 검색한 뒤 **초기화**를 누르면 다시 처음 화면으로 돌아갑니다.

## 최초 실행

등록된 폴더가 없으면 다음 안내가 나옵니다.

- 제목: 인덱싱할 폴더를 선택하세요
- 본문: 검색할 PDF, Word, 한글(HWP) 파일이 들어 있는 폴더를 선택하세요. 파일 이름만이 아니라 계약서 본문·스캔 OCR 글자까지 이 컴퓨터에서만 찾아 둡니다.
- 보조: 다른 컴퓨터에서는 폴더 이름과 위치가 다를 수 있습니다. 그 컴퓨터에 있는 문서 폴더를 새로 선택하면 됩니다.

글자가 들어 있는 PDF는 기존 텍스트만 읽습니다. 스캔(이미지) 페이지는 설치본에 들어 있는 Tesseract로 OCR합니다. Word(`.docx`/`.doc`)와 한글(`.hwp`/`.hwpx`)은 파일 안의 글자를 읽습니다. 사용자는 페이지가 하나씩 뜨는 화면을 보지 않습니다.

Windows 설치본에는 한국어·영어 언어팩이 같이 들어 있습니다. 따로 Tesseract를 설치하지 않아도 됩니다.

macOS에서 `dotnet run`으로 개발할 때만 스캔 OCR이 필요하면:

```bash
brew install tesseract tesseract-lang
```

## 개발용으로 실행

필요 환경: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows 또는 macOS)

macOS Homebrew 예:

```bash
brew install --cask dotnet-sdk
brew install tesseract tesseract-lang
```

```bash
git clone https://github.com/boam79/DocuLensLocal.git
cd DocuLensLocal
dotnet test
dotnet build
dotnet run --project src/DocuLensLocal.App
```

Windows PowerShell에서도 같은 `dotnet` 명령을 씁니다. Mac에서는 설치 없이 이 명령으로 앱을 띄워 기능을 확인하면 됩니다.

검색 결과를 **두 번 클릭**하면 원본 파일을 엽니다.

솔루션 파일은 `DocuLensLocal.slnx` 입니다.

| 프로젝트 | 역할 |
|---|---|
| `src/DocuLensLocal.App` | 화면 (Windows·macOS에서 실행) |
| `src/DocuLensLocal.Worker` | 인덱싱·OCR 백그라운드 |
| `src/DocuLensLocal.Core` | 공통 로직 (본문 추출·OCR·검색) |
| `tests/DocuLensLocal.Core.Tests` | 테스트 |

검색 인덱스와 설정은 OS 로컬 앱 데이터에 둡니다. 원본 문서는 읽기만 합니다.

- Windows: `%LOCALAPPDATA%\DocuLensLocal\userdata`
- macOS: `~/Library/Application Support/DocuLensLocal/userdata`

Windows 설치 파일을 이 저장소에서 다시 만들려면:

```powershell
./scripts/pack.ps1
```

결과는 `artifacts/Releases/DocuLensLocal-win-Setup.exe` 입니다.
