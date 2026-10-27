# Screenshots

These pictures are not in the repository yet. Shellmason is a Windows Presentation Foundation app, and it was not run to produce them. Do not add a mockup, a diagram, a cropped picture from another program, or an image generated to look like the window.

Capture a real Shellmason window. The title bar says Shellmason. The program file is `PsGuiBuilder.exe`. The default window is 1120 by 780. Save PNG files in this folder, using the names below, then embed them in the Screenshots section of the repository README.

Use a folder you are willing to show in public. The sample path below is `C:\Users`. If that listing would show names you do not want published, pick another folder and say which one in the commit.

Do not include a password, a credential dialog, a token, or a UAC prompt that shows an account name you do not want public.

## 1. Cascading menus — `cascading-menus.png`

The shot is the menus, with a command not selected yet.

1. Launch Shellmason.
2. Leave **Favorites / Common** selected.
3. Open **I Want To:** and choose **Files and folders**.
4. Open **Then:** and leave the list open. The list includes **List a folder**, **Copy an item**, and **Move an item**.
5. Do not choose a command. The Parameters card should still say "Choose a command to fill in its parameters."
6. Capture the window so both labels and the open **Then:** list are readable.

## 2. Sentence-style parameter form — `sentence-form.png`

1. From the menus above, choose **Files and folders**, then **List a folder**.
2. The Parameters card shows the sentence "I want the contents of {folder}, recurse {include subfolders}, hidden {include hidden items}", with a text field, two checkboxes, and a short label under each control.
3. Type `C:\Users` in the folder field (or your public sample folder).
4. Include the **PowerShell** card. The preview should show a quoted `Get-ChildItem -Path "..."` command.
5. Leave **Run as administrator** unchecked.

## 3. Output pane — `output-pane.png`

1. With the sentence from the previous shot filled in, click **Run**. Leave **Run as administrator** unchecked so Windows does not open UAC.
2. Wait until the dark pane at the top, labeled **Output**, shows the listing and the status line says the run completed.
3. Include enough of the sentence and the PowerShell preview that a reader can see which command produced the listing.

## Optional GIF — `cascade-to-output.gif`

One short recording of the same path: **I Want To:** Files and folders, **Then:** List a folder, fill the folder, **Run**, output appears. A few seconds is enough. Skip it if you only want the three stills.
