---
name: USB Device Database Addition
about: Request that a USB radio interface or serial adapter be added to the known-device list. No coding needed — just plug in your device and follow the steps below.
title: "[USB DEVICE] "
labels: usb-database, community
assignees: Computer-Tsu
---

## How to Find Your VID and PID (Step-by-Step)

You do not need to know what VID and PID mean — just follow these steps:

1. **Connect** your USB device to your Windows PC.
2. **Right-click** the Start button (bottom-left) and choose **Device Manager**.
3. Look for your device under one of these sections:
   - **Ports (COM & LPT)** — for serial/CAT cables
   - **Sound, video and game controllers** — for audio interfaces
4. **Right-click** your device name and choose **Properties**.
5. Click the **Details** tab.
6. In the **Property** dropdown, choose **Hardware IDs**.
7. You will see a value like: `USB\VID_10C4&PID_EA60`
8. The 4 characters after `VID_` are your **VID** (e.g. `10C4`)
   The 4 characters after `PID_` are your **PID** (e.g. `EA60`)
9. Fill in the fields below with those values (uppercase).

---

## Device Information

| Field             | Your Answer                                     |
|------------------|-------------------------------------------------|
| Manufacturer      |                                                 |
| Product Name      |                                                 |
| VID (4 hex chars) | e.g. `10C4`                                     |
| PID (4 hex chars) | e.g. `EA60`                                     |
| Device Type       | Serial (COM port) / Audio / Both                |
| Radio Interface?  | Yes — used with amateur or professional radio   |
|                   | No — general purpose adapter                    |
| Baud Rate         | e.g. 9600, 115200, or "not applicable" (audio)  |
| Flow Control      | None / Hardware (RTS/CTS) / Software (XON/XOFF) |

---

## Compatible Digital Mode Software

Check all that apply:

- [ ] FT8 / FT4 (WSJT-X or JTDX)
- [ ] Winlink (RMS Express / Winlink Express)
- [ ] Fldigi
- [ ] JS8Call
- [ ] VARA HF
- [ ] VARA FM
- [ ] Direwolf
- [ ] APRS (any software)
- [ ] Hamlib / flrig (rig control)
- [ ] Other: ___

---

## Additional Notes

Any notes about this device — for example:

- Driver requirements or known Windows driver issues
- Which specific radios this cable/interface is used with
- Any special configuration needed (e.g. specific baud rate required)

---

## Contribution Checklist

- [ ] I have provided the 4-character VID and 4-character PID from Device Manager
- [ ] I have confirmed the device type (serial or audio)
- [ ] I have indicated whether this is a radio interface
- [ ] I understand that by submitting this, I agree to the [CLA](../../CLA.md)
