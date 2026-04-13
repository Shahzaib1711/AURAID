# Option A: Arabic font for the menu

Follow these steps to create an Arabic TMP Font Asset and assign it so the menu can show Arabic text (no more squares or missing-glyph warnings).

---

## Step 1: Get the font file

1. Go to **https://fonts.google.com/noto/specimen/Noto+Sans+Arabic**
2. Click **Download family** and unzip.
3. In your project, create a folder if needed (e.g. `Assets/Fonts/`).
4. Copy **NotoSansArabic-Regular.ttf** (or the .ttf you want) into that folder.

---

## Step 2: Create the TMP Font Asset

1. In Unity menu: **Window → TextMeshPro → Font Asset Creator**
2. **Source Font File:** click the circle and select your font (e.g. `NotoSansArabic-Regular`).
3. **Character Set:** choose **Unicode Range (Hex)**.
4. In **Character Sequence**, enter (commas only, **no spaces** — spaces can cause FormatException):  
   **0600-06FF,0020,005F,FE70-FEFF**  
   - `0600-06FF` = Arabic  
   - `0020` = space  
   - `005F` = underscore (stops "Underline is not available" warning)  
   - `FE70-FEFF` = Arabic Presentation Forms (so letters connect as proper words)  
   Use **2048 x 2048** atlas if you get "missing characters" after generating.
5. **Atlas Resolution:** leave **1024 x 1024** (or use **2048** if you get missing characters later).
6. **Render Mode:** **SDFAA** (default).
7. Click **Generate Font Atlas**. Wait for it to finish.
8. Click **Save** or **Save as** and save the asset, e.g.:
   - `Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansArabic SDF.asset`  
   or any folder (e.g. `Assets/Fonts/NotoSansArabic SDF.asset`).

---

## Step 3: Assign on UIFlow Manager

1. In the **Hierarchy**, select the GameObject that has the **UIFlow Manager** component (usually on the main menu / canvas).
2. In the **Inspector**, find the section **Fonts for Arabic (fixes missing glyphs)**.
3. Drag your new font asset (e.g. **NotoSansArabic SDF**) into **Arabic Font For Menu**.
4. (Optional) Set **Default Font For Menu** to **LiberationSans SDF** so switching back to English uses the correct font.

---

## Fix "Underline is not available" (if you still see it)

TMP asks the current font for an underline character as soon as the font is set, even if the text has no underline. So the Arabic font must contain that character.

1. Open **Font Asset Creator** again and select your **Noto Sans Arabic** source font.
2. **Character Set:** **Unicode Range (Hex)**.
3. **Character Sequence:** use **0600-06FF,0020,005F** (no spaces after commas).
4. **Generate Font Atlas** → **Save** over your existing **NotoSansArabic SDF**.

After that, the underline warning should stop.

---

## Done

Enter Play mode, choose **Arabic** on the language screen, then open Mode/Scenario. The menu should show Arabic text correctly and the missing-character warnings should stop.
