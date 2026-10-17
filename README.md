<h1 align="center">
  <img src="images/logo.png" alt="GorillaHurouf" width="460">
</h1>

<p align="center"><b>Arabic that just works in Gorilla Tag.</b> · غوريلا حروف</p>

![Arabic before and after](images/before-after.png)

Gorilla Tag can't show Arabic right. The letters come out separate and backwards. GorillaHurouf fixes that. It is a small library for BepInEx mods. You can fix one text, or fix every text in the game with one line.

## What it can do

- **Fix Arabic text.** The letters join, the order is right, and English and numbers mix in fine.
- **Auto-fix.** One line, and all text in the game that has Arabic shows right.
- **Notifications.** Show an Arabic message in front of the player, in any colour.
- **Message of the day.** Put your own Arabic text and colour on the game's message board.
- **Translations.** Keep English and Arabic texts in a JSON file.
- **Hijri date.** Show today's Hijri date in Arabic.

It does nothing on its own. A mod has to use it.

## Install

You only need this if a mod asks for it.

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for Gorilla Tag. Run the game once.
2. Download `GorillaHurouf.dll` from the Releases page.
3. Put it in `Gorilla Tag\BepInEx\plugins`.

## Use it in your mod

### 1. Add it

Add `GorillaHurouf.dll` to your project, the same way you add BepInEx. Then make your mod depend on it, so BepInEx loads it first:

```csharp
[BepInDependency("dev.musaed.gorillahurouf")]
```

Players need `GorillaHurouf.dll` in `BepInEx\plugins` next to your mod.

### 2. Choose what you need

You don't need all of it. Find what you want to do:

| You want to... | Use this | When |
|---|---|---|
| Show Arabic in your own text | `label.SetArabic("...")` | Each time you set the text. Use it instead of `label.text = "..."` |
| Fix all text in the game | `Arabic.EnableAutoFix()` | One time, after the game starts. `Start` is a good place |
| Tell the player something | `Arabic.Notify("...", NotifyKind.Success)` | Any time. It waits if the game is still loading |
| Show your message on the message board | `Arabic.SetMessageOfTheDay("...", Color.green)` | Any time. `Arabic.ClearMessageOfTheDay()` puts the game's message back |
| Use English and Arabic texts | `var tr = Arabic.Translator("MyMod");` then `tr.T("key")` | Load your JSON file one time at the start. Then use `T` when you show a text |
| Show the Hijri date | `Arabic.Hijri()` | When you build the text with the date |
| Skip one text in auto-fix | `Arabic.SkipAutoFix(label)` | Right after you make the text |

`SetArabic` fixes one text. `EnableAutoFix` fixes all text in the game, so you can write `label.text = "..."` like normal. Use only one of them for the same text.

### 3. A full mod in one file

```csharp
using BepInEx;
using GorillaHurouf;
using TMPro;
using UnityEngine;

[BepInPlugin("com.example.mymod", "MyMod", "1.0.0")]
[BepInDependency("dev.musaed.gorillahurouf")]
public class MyMod : BaseUnityPlugin
{
    void Start()
    {
        Arabic.Notify("هلا والله، المود شغال", NotifyKind.Success);       // a message in front of the player
        Arabic.SetMessageOfTheDay("يا هلا والله فيكم", Color.green);      // your text on the message board
        // or, to fix all text in the game without changing your code: Arabic.EnableAutoFix();
    }

    // wherever you set one of your own labels:
    public void ShowScore(TMP_Text label, int score)
    {
        label.SetArabic("نقاطك: " + score);                               // instead of label.text = ...
    }
}
```

Every call has a short comment in `Arabic.cs` that says what it does.

## Licence

MIT, see [LICENSE](LICENSE). Copyright (c) 2026 Musaed.

The font inside is Noto Sans Arabic. It keeps its own licence (SIL Open Font License 1.1, see [Fonts/OFL.txt](Fonts/OFL.txt)).
