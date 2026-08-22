# DocuLens Local

Windows 10/11용 **로컬 문서 검색기**입니다. 탐색기 파일명 검색과 달리, 계약서·MOU를 원래 폴더에 둔 채 **본문과 스캔 글자까지** 이 컴퓨터에서만 인덱싱합니다. 문서 원문은 외부 서버로 보내지 않습니다.

지원 파일: **PDF, Word(`.docx`/`.doc`), 한글(`.hwp`/`.hwpx`), Excel(`.xlsx`/`.xlsm`/`.xls`)**. txt·이미지 같은 다른 파일은 건너뜁니다.

개발은 Windows와 macOS 모두에서 `dotnet test` / `dotnet run`으로 합니다. Mac용 설치 파일은 만들지 않습니다.

- 설치 대상: Windows 10/11 64비트
- 스택: .NET 10, Avalonia, PdfPig, HwpLibSharp, 내장 Tesseract(kor/eng), 로컬 SQLite
- 저장소: https://github.com/boam79/DocuLensLocal
- 문서: [docs/](docs/README.md) (사용 안내, 검색, 인덱싱, 설치, 개발, 고도화)

## 탐색기와 다른 점

| | 파일 탐색기 / Spotlight | DocuLens Local |
|---|---|---|
| 찾는 대상 | 주로 파일 이름 | 파일 이름 + PDF·Word·한글·Excel 본문 |
| 스캔(이미지) PDF·Word·한글·Excel | 거의 못 찾음 | 글자가 없으면 로컬 OCR |
| 검색 결과 | 경로만 | 본문 **근거 문장** |
| 원문 유출 | OS 인덱서에 따라 다름 | PC 밖으로 안 보냄 |
| 원본 파일 | — | 읽기만, 수정 없음 |

예: 파일명이 `내부문서.docx`여도 본문에 「버스 광고 계약」이 있으면 `버스 광고 찾아줘`로 찾습니다. PDF·HWP·Excel도 같습니다.

## 설치 파일 받기

설치 파일 직접 주소:

- https://github.com/boam79/DocuLensLocal/releases/download/v0.1.29/DocuLensLocal-win-Setup.exe

GitHub Releases 페이지의 **Assets**가 잠시 돌아가 보일 수 있습니다. 스피너가 끝나지 않으면 위 주소를 브라우저에 붙여 넣으세요.

- 릴리스 목록: https://github.com/boam79/DocuLensLocal/releases
- 최신 릴리스: https://github.com/boam79/DocuLensLocal/releases/latest
- 파일 이름: `DocuLensLocal-win-Setup.exe`

받은 파일을 실행하면 설치 화면(진행 표시)이 나옵니다. 코드 서명이 없어 Windows가 경고를 띄울 수 있습니다. 추가 정보를 연 뒤 실행을 선택하세요.

설치가 끝나면 **인덱싱할 폴더를 선택하세요** 창이 뜹니다. 빈 화면이 아니라 이 안내가 보여야 설치가 끝난 것입니다.

다른 컴퓨터에 쓰려면 그 컴퓨터에도 이 설치 파일을 받아 설치하고, 그 PC에서 열 수 있는 문서 폴더를 선택하세요. 폴더 이름이 `계약서`가 아니어도 됩니다.

Word·한글·Excel은 파일 안 글자를 읽고, 글자가 거의 없으면 들어 있는 큰 그림도 OCR합니다. 스캔 계약서·견적을 Word/HWP/Excel로 저장한 경우 **폴더** → **처음부터 다시 읽기**를 한 번 누르세요. 원본 파일은 그대로 두고, 이 앱의 검색 목록만 지운 뒤 폴더를 다시 읽습니다.

이미 인덱싱한 폴더에 Excel만 빠져 있었다면 앱을 다시 켜면 **새로 넣은 파일만 읽는 중**이 뜨고 엑셀을 읽습니다. 예전에 엑셀·한글이 파일 이름만 들어가고 본문이 비어 있었다면 이번 버전에서 다시 읽습니다. 앱을 켠 채 폴더에 `.xlsx`/`.hwp`를 넣으면 자동으로 그 파일만 읽습니다.

새 버전이 있으면 앱을 켤 때 팝업이 뜨고, **무엇이 바뀌었는지**가 함께 보입니다. **확인**을 누르면 업데이트하고, 다시 켜진 뒤 **업데이트 내역**을 보여 줍니다. 정보 탭 **업데이트**도 같습니다. 인덱싱 중에 업데이트해도, 다시 시작한 뒤 **남은 파일부터 이어서** 읽습니다.

검색 결과에서 **열기**를 누르면 그 파일이 열립니다. **폴더에서 보기**는 탐색기에서 그 파일 위치를 보여 줍니다. 근거 문장의 검색어는 굵게 표시됩니다.

검색창 아래 **PDF / WORD / HWP / EXCEL**을 누르면 그 종류만 나옵니다. **여러 개를 함께** 누를 수 있습니다. 같은 칸을 다시 누르면 그 종류는 빠지고, 하나도 안 고르면 모든 종류를 찾습니다.

검색한 뒤 검색창 옆 **초기화**를 누르면 검색어만 지워집니다. 눌러 둔 종류는 그대로입니다.

폴더에 PDF·Word·한글·Excel을 **나중에 더 넣으면** 앱이 켜져 있는 동안 **자동으로** 그 파일만 읽습니다. 이미 읽은 파일은 그대로 둡니다. 바로 확인하고 싶으면 **폴더** → **새 파일만 읽기**를 눌러도 됩니다. **처음부터 다시 읽기**는 검색 목록을 지운 뒤 전부 다시 읽을 때만 쓰세요.

## 최초 실행

등록된 폴더가 없으면 다음 안내가 나옵니다.

- 제목: 인덱싱할 폴더를 선택하세요
- 본문: 검색할 PDF, Word, 한글(HWP), Excel 파일이 들어 있는 폴더를 선택하세요. 파일 이름만이 아니라 계약서 본문·스캔 OCR 글자까지 이 컴퓨터에서만 찾아 둡니다.
- 보조: 다른 컴퓨터에서는 폴더 이름과 위치가 다를 수 있습니다. 그 컴퓨터에 있는 문서 폴더를 새로 선택하면 됩니다.

글자가 들어 있는 PDF·Word·한글·Excel은 기존 텍스트만 읽습니다. 스캔(이미지) 페이지나 그림으로만 된 Word/HWP/Excel은 설치본에 들어 있는 Tesseract로 OCR합니다. 사용자는 페이지가 하나씩 뜨는 화면을 보지 않습니다.

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
