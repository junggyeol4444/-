# MYVOCAL Studio

MYVOCAL Studio는 **C#/.NET 8 WPF로 작성된 Windows 네이티브 데스크톱 프로그램**입니다. 웹사이트, Python, 브라우저 또는 로컬 서버를 사용하지 않습니다.

## 기술 선택

프로덕션 Windows 애플리케이션에 맞춰 다음 구조를 사용합니다.

- **C# / .NET 8:** 정적 타입, 비동기 작업, 취소와 예외 처리
- **WPF:** Windows 네이티브 데스크톱 UI와 하드웨어 가속 렌더링
- **자체 프로젝트 계층:** UI, 프로젝트 모델, 저장 서비스, 오디오 렌더러 분리
- **Self-contained win-x64 배포:** 대상 PC에 .NET이나 Python이 없어도 단일 EXE 실행
- **Atomic save:** 임시 파일을 쓴 다음 교체해 프로젝트 손상 위험 감소

## Windows EXE 빌드

Windows 10/11과 .NET 8 SDK가 설치된 PC에서:

```bat
build_windows.bat
```

스크립트는 Restore → Release Build → Smoke Test → Self-contained Publish를 순서대로 수행합니다. 하나라도 실패하면 EXE를 만들지 않고 오류 코드로 종료합니다.

결과 파일:

```text
dist\MYVOCAL-Studio.exe
```

이 EXE는 self-contained 단일 파일이므로 대상 PC에 Python 또는 .NET Runtime을 별도로 설치할 필요가 없습니다.

GitHub에서는 **Actions → Build and verify Windows EXE**를 실행해 검증된 `MYVOCAL-Studio-win-x64` 아티팩트를 받을 수 있습니다.

## 현재 실제 구현 범위

- WPF 네이티브 프로젝트 창과 Windows 파일 대화상자
- 멀티트랙 타임라인, 클립 선택·드래그·스냅
- 프로젝트 트랙을 임시 WAV로 렌더링한 뒤 Windows Media Foundation으로 수행하는 실제 미리듣기
- 재생 헤드와 실제 오디오 위치 동기화, 재생·일시정지·정지·되감기
- Vocal, Instrument, Audio, Video 데이터 모델
- Mono/Stereo 16-bit PCM WAV 파일 가져오기와 타임라인 배치
- 가져온 WAV의 샘플레이트 변환 재생 및 프로젝트 믹스 포함
- Track Mute/Solo 및 트랙 추가
- 클립 이동, 트랙 추가, WAV 가져오기, Mute/Solo/Volume, AI 편집의 50단계 Undo/Redo
- Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, Ctrl+S 및 Space 키보드 단축키
- 저장되지 않은 변경 상태 표시와 종료 전 손실 방지 확인
- 30초 간격 사용자별 원자적 자동 저장과 비정상 종료 후 프로젝트 복구
- 가사, Piano Roll, Automation, MV Storyboard 편집 화면
- AI Producer 명령에 따른 Drum/Harmony 클립 변경
- `.myvocal` 프로젝트의 원자적 저장 및 다시 열기
- Track Mute/Solo/Volume과 클립 구간을 반영하는 44.1 kHz, 16-bit, Stereo 비동기 WAV 렌더링
- 단일 Windows x64 EXE 배포

## 아직 실제 Provider가 필요한 범위

다음은 UI 문구만으로 완성됐다고 주장하지 않습니다.

- 실제 보이스 모델 학습과 Singing 합성
- LLM 기반 작사·작곡·편곡
- Suno 및 기타 Music API 연동
- 사람·캐릭터 학습과 Identity Lock 생성 모델
- 2D/3D 모션, Lip Sync, 영상 생성 및 MP4 인코딩
- GPU 작업 큐, 캐시, 클라우드 동기화와 권한 검증

이 기능들은 각각 별도의 모델 또는 외부 Provider가 필요합니다. 현재 코드는 이들을 붙일 수 있는 데스크톱 편집기와 프로젝트 기반을 제공하지만, 구현되지 않은 AI 기능을 완성품이라고 표시하지 않습니다.

## 개발 명령

```powershell
dotnet restore MYVOCAL-Studio.sln
dotnet build MYVOCAL-Studio.sln -c Release
dotnet run --project tests/MyVocalStudio.Tests -c Release
dotnet run --project src/MyVocalStudio
```
