# Predict – zárt tesztelési javítások

## Forrás és hatókör
- Forrás: `\\lenovoMint\RainbowTransfer\Predict\Zárt tesztelés`, teljes `feedback.md.txt` és három screenshot átnézve.
- Kiadási baseline: commit `9c15f431f07e460c6cfd24f9c79ce85df5761788`, tree `c74e2db67ec9920f39d9b4922af0fd0e4ad7f1b9`.
- Független szülői tree-rekonstrukció: 109 fájl, pontos egyezés a release receipt-tel; másolás utáni tree is egyezik.
- Elkülönített munkapéldány: `C:/Users/corta/Projects/Predict-closed-testing-fixes-20261103`.
- A régi `Projects/Predict` net9 checkout, a kiadási snapshotok és a felhasználói history érintetlenek.
- Nincs Play-publikálás, production signing, verzióemelés vagy automatikus készre zárás.

## Notion (minden kártya a Predict projekthez kapcsolva, In progress)
- 1+3. súlybevitel: `40dde746-578b-814e-86d6-d321d9643835`.
- 2. ismétlésszám: `40dde746-578b-819d-8b20-cecd2045ae0e`.
- 4. history: `40dde746-578b-8123-bb42-c0a0bbbc0efd`.
- 5+6. téma/fókusz: `40dde746-578b-8133-a909-d7074dc95b4c`.
- Mind a négy létrehozott oldal tartalma és projekt/státusz tulajdonsága visszaolvasva, ellenőrizve.

## Súlybevitel és reps – implementáció
- Kezdő pont/vessző előtt látható 0 jelenik meg.
- Legfeljebb két tizedes; hibás karakter vagy több tizedes esetén a teljes szerkesztés elutasítása, előző érték megtartása és látható hiba. Nincs csendes levágás.
- Kalkuláció szigorú, kultúrafüggetlen parse, egységenkénti felső korlát és pozitív véges szám ellenőrzése.
- Command CanExecute és frissülő nézetmodell-tulajdonságok; 1-nél mínusz és 20-nál plusz disabled, 0.35 opacity.
- Üres repsnél plusz 1-re állít, mínusz disabled; hibás/nem támogatott reps esetén mindkettő disabled.

## Végrehajtott tesztek
- `InputFeedback` tényleges production MainViewModel + MVVM generátor + calculator/model linked C# harness; platformperemek doubles.
- RED: 20 passed, 36 failed (en-US és hu-HU).
- GREEN: 56 passed, 0 failed, compiler warning nélkül.
- Reps XAML disabled-state wiring: RED 1 failure → GREEN 1 passed.
- Logok: `C:/Users/corta/AppData/Local/hermes/cache/scratch/predict-input-red.log`, `predict-input-green.log`.
- Lokális teszt SDK: 9.0.319, munkakönyvtár `C:/Users/corta/Shared`, csak a child process környezetében `MSBuildEnableWorkloadResolver=false`; ez kizárólag a net9 linked harnesshez alkalmazva. Az Android projekt pinje nem változott.

## Integrált implementáció és regresszió
- Mind a hat visszajelzési pont javítása integrálva, négy Notion-kártyán vezetve.
- History: kanonikus kg → a rekord IsLbs szerinti kijelzés; input history/detail/share 0.##. Nincs adatmigráció vagy törlés. Legacy JSON load/roundtrip és save-megőrzés tesztelt.
- History C# RED reprodukálta a hibás lbs-címkét és pontosságvesztést; GREEN. A valódi megosztó parancs kimenő szövege, hu-HU formázás és 360 formula/estimate/percentage baseline kombináció sikeres; számított eredmények továbbra is egy tizedesek.
- Android: súlymezőre célzott native background eltávolítás; HideSoftInputOnTapped; Completed és IME látható→rejtett átmenetnél unfocus. Inicializáláskori rejtett billentyűzet nem unfocusol, focus cycle reset; observer detach handler-cserénél/eltűnésnél/unloadnál. Nincs globális mapper vagy új touch-interceptor.
- Szülői XAML-integráció: RED hiányzó bekötés → GREEN. Öt statikus focus/wiring teszt; nem natív runtime-bizonyíték.
- Teljes végleges regresszió: 14 valódi C# parancs exit0; Python unittest discovery:93 teszt OK, skipped1.
- Tartós logok, eredmények, teljes before/after SHA256 manifest és diff: `C:/Users/corta/Shared/Predict-closed-testing-review-20261103`.

## Független review
- `deleg_55de8596`: PASS, security_concerns=[], logic_errors=[]. Mind a 16 változott/új út és a teljes baseline manifest ellenőrzött; forrás- és diffhash egyezett. Hat Python wiring check függetlenül végrehajtva, sikeres.
- Az eredeti `review.diff`, `manifest.json` és `review-verdict.json` megőrizve. A jelen dokumentum ezen kiegészítése review UTÁNI, kizárólag dokumentációs delta; a production és tesztbájtok nem változtak.
- Nyitott review-javaslat: natív Entry binding esetén ellenőrizni a látható szöveget is (kezdő vessző és elutasított harmadik tizedes), nem csak a viewmodelt.

## Hátralévő kapuk / korlátok
- Pinned SDK10.0.403 / MAUI10.0.120 / Android36 teljes fordítása még nincs. Helyben csak SDK9.0.319 és Android35; a linked net9 tesztek sikeréből nem következik az app compile-ja. A kiadási pin változatlan.
- A Lenovo nem előkészített builder. A meglévő GitHub Actions Dev APK út publikus repo standard windows-latest runnerrel ingyenes; távoli forrásbranch és scoped cloud-build jóváhagyás még kell. Nem indult távoli build vagy forrásfeltöltés.
- Telefonos világos/sötét, Back/keyboard-close/background tap, rapid refocus, navigáció/handler-recreation, első-tap slider/reps/Estimate és TalkBack ellenőrzés nyitott; floating/OEM keyboard kockázat külön.
- A helyi Git Windows Access denied hibája reprodukálva; nincs biztonsági beállítás- vagy ACL-módosítás.
- Nincs új APK, production signing, verzióemelés vagy Play-publikálás. A négy Notion-kártya In progress marad a compile és elfogadás végéig.
