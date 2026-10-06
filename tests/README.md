# 게임 없이 돌리는 확인기

CS2가 깔린 PC 없이 앱을 확인한다. 게임용 PC를 못 켜는 날에도 돌릴 수 있고,
손으로 눌러서는 순서를 밟아야 보이는 것(예: 난이도를 쉬움으로 두고 → 끄고 → 다시 켜기)을 한 번에 본다.

실제로 이 확인기로 첫 공개판부터 있던 버그를 잡았다 — 봇 난이도를 쉬움으로 두면 다시 켤 때 보통으로 돌아가던 것(1.1.1에서 고침).

## 돌리기

```
dotnet build tests/cs2-dummy/cs2-dummy.csproj
dotnet build tests/Checks/Checks.csproj
tests/Checks/bin/Debug/net48/Checks.exe
```

전부 통과하면 종료 코드 0, 하나라도 실패하면 1이다.

일부만 돌리려면 `lang`·`state`·`update` 중 하나를 인자로 준다.

```
tests/Checks/bin/Debug/net48/Checks.exe lang
tests/Checks/bin/Debug/net48/Checks.exe state <가짜 cs2.exe 경로>   (경로는 생략 가능)
```

## 무엇을 보나

| 모드 | 보는 것 |
| --- | --- |
| `lang` | 한국어·영어 전환. 고른 값이 남는지, 저장값에 보이는 이름이 안 들어가는지, 1.0.2 형식 설정이 이어받아지는지 |
| `state` | 네 상태의 띠 글·띠 색·버튼 글 (꺼짐 / 앱 밖에서 켬 / 서버 없음 / 서버 열림), 탈환에서 봇 칸이 잠기는지, 주소·참가 링크 |
| `update` | 새 버전 번호를 받아 오는지, **그 번호가 창에 실제로 뜨는지**, 누르면 가는 곳이 배포 페이지가 아닌지 |

## 어떻게 게임 없이 되나

앱이 게임에서 보는 것은 셋뿐이다.

1. `cs2`라는 이름의 프로세스가 있나 (`Process.GetProcessesByName`)
2. 그 명령줄에 `-condebug`가 있나 (WMI)
3. `console.log`에 프로세스 시작 시각 뒤의 `ServerSteamID=` 줄이 있나

그래서 **이름만 `cs2`인 빈 프로세스**(`tests/cs2-dummy`, 아무 일도 안 하고 잠만 잔다)와
손으로 쓴 가짜 `console.log`로 네 상태가 전부 재현된다.
설치 폴더도 `game\csgo\maps`에 빈 `.vpk`만 있으면 앱이 받아들인다.

## 함정 (다 겪은 것들)

- **`PerformClick()`은 창이 안 보이면 아무 일도 안 한다**(`CanSelect`가 false). 조용히 통과하므로
  「전환이 되는 줄」 알고 넘어간다. 창을 안 띄우는 확인에서는 `Control.OnClick`을 리플렉션으로 올린다.
- **가짜 로그의 날짜는 `CultureInfo.InvariantCulture`로 쓴다.** 커스텀 형식의 `/`는 문화권 날짜
  구분자로 치환돼 한국어 환경에서 `10-04`가 나온다. 앱 파서는 Invariant라 `/`를 글자 그대로 본다
  (게임이 쓰는 실제 형식이 `/`다).
- **앱의 ▶ 버튼은 절대 누르지 않는다.** `Cs2Process.CloseAll()`이 `cs2` 이름의 프로세스를 전부 죽인다.
- **`APPDATA` 환경변수로는 설정 위치를 못 옮긴다**(`GetFolderPath`는 셸에서 읽는다). 그래서 확인기가
  실제 `%APPDATA%\CS2PracticeHost\settings.json`을 쓴다 — 시작할 때 내용을 들고 있다가 끝나면
  그대로 돌려놓는다(`Fake.KeepRealSettings`).
- **번호를 받아 오는 것과 창에 올리는 것은 다른 자리다.** 창을 안 띄우면 `Shown`에 걸린 쪽이
  아예 안 돌아간다. 앞의 것만 보고 「확인 완료」로 닫았다가 놓친 적이 있다.

## update 모드의 버전

확인기 자신의 어셈블리 버전을 「지금 쓰는 사람의 판」으로 쓴다. `csproj`가 `1.0.0`이라 늘 최신보다 낮고,
그래서 「새 버전 있음」이 나와야 맞다. 반대 경우(최신을 쓰는 사람에게 안 뜨는지)는 이렇게 본다.

```
dotnet build tests/Checks/Checks.csproj -p:Version=99.0.0
```

GitHub API는 IP당 시간당 60회라, 짧은 시간에 여러 번 돌리면 한도에 걸려 `null`이 나올 수 있다.

## 앱 빌드와 섞이지 않게

앱 `csproj`는 폴더 전체의 `.cs`를 긁어가므로 `tests/**`를 `Compile Remove`로 빼 뒀다.
테스트를 다른 이름의 폴더로 옮기면 그 줄도 같이 고쳐야 한다.
