import csv
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main():
    if len(sys.argv) < 3:
        raise SystemExit("Usage: python make_preview_contact_sheet.py <preview_dir> <output_png>")

    preview_dir = Path(sys.argv[1])
    output_png = Path(sys.argv[2])
    rows = list(csv.DictReader((preview_dir / "metadata.csv").open(encoding="utf-8")))

    frame_w, frame_h = 320, 240
    label_h = 42
    first_pattern = sorted(preview_dir.glob(f'{int(rows[0]["index"]):03d}_*_f*.png')) if rows else []
    cols = max(1, len(first_pattern))
    sheet_w = frame_w * cols
    sheet_h = (frame_h + label_h) * len(rows)
    sheet = Image.new("RGB", (sheet_w, sheet_h), (35, 35, 35))
    draw = ImageDraw.Draw(sheet)

    try:
        font = ImageFont.truetype("arial.ttf", 16)
    except Exception:
        font = ImageFont.load_default()

    for row_index, row in enumerate(rows):
        action_index = int(row["index"])
        pattern = f"{action_index:03d}_*_f*.png"
        images = sorted(preview_dir.glob(pattern))
        y = row_index * (frame_h + label_h)

        label = f'{row["index"]} | {row["action"].split("|")[-1]} | {row["frames"]} frames'
        draw.rectangle((0, y, sheet_w, y + label_h), fill=(20, 20, 20))
        draw.text((8, y + 10), label, fill=(230, 230, 230), font=font)

        for col, image_path in enumerate(images[:cols]):
            image = Image.open(image_path).convert("RGB")
            sheet.paste(image, (col * frame_w, y + label_h))

    output_png.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_png)


if __name__ == "__main__":
    main()
