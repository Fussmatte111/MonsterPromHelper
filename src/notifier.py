"""Windows toast notifications."""

from __future__ import annotations

import sys


def notify(title: str, message: str, *, duration: str = "short") -> None:
    try:
        from winotify import audio, Notification

        toast = Notification(
            app_id="Monster Prom Helper",
            title=title,
            msg=message,
            duration=duration,
        )
        toast.set_audio(audio.Default, loop=False)
        toast.show()
    except Exception:
        # Fallback: PowerShell balloon (no extra deps if winotify missing)
        safe_title = title.replace("'", "''")
        safe_msg = message.replace("'", "''")
        import subprocess

        ps = (
            f"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, "
            f"ContentType = WindowsRuntime] | Out-Null; "
            f"$template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent("
            f"[Windows.UI.Notifications.ToastTemplateType]::ToastText02); "
            f"$text = $template.GetElementsByTagName('text'); "
            f"$text[0].AppendChild($template.CreateTextNode('{safe_title}')) | Out-Null; "
            f"$text[1].AppendChild($template.CreateTextNode('{safe_msg}')) | Out-Null; "
            f"$toast = [Windows.UI.Notifications.ToastNotification]::new($template); "
            f"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("
            f"'Monster Prom Helper').Show($toast);"
        )
        subprocess.run(
            ["powershell", "-NoProfile", "-Command", ps],
            check=False,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        print(f"[notify] {title}: {message}", file=sys.stderr)
