#!/usr/bin/env python3
"""
Export Depth-Anything-V2 model to ONNX format for use with DepthClockWallpaper.

This script downloads the pre-trained Depth-Anything-V2 model and exports it
to ONNX format, which can then be used by the C# application via ONNX Runtime.

Requirements:
    uv sync  # Automatically installs all dependencies from pyproject.toml

Usage:
    uv run python export_model.py [--model-size small|base|large]
"""

import argparse
import sys
import torch
import onnx
from pathlib import Path

# Add the cloned depth_anything_v2 directory to Python path
sys.path.insert(0, str(Path(__file__).parent.parent / "depth_anything_v2"))


def download_model(model_size="small"):
    """
    Downloads the Depth-Anything-V2 model from Hugging Face.

    Args:
        model_size: 'small' (24.8M), 'base' (97.5M), or 'large' (335.3M)

    Returns:
        Loaded PyTorch model
    """
    print(f"Downloading Depth-Anything-V2 {model_size.upper()} model...")

    # Map size to encoder names
    encoder_map = {"small": "vits", "base": "vitb", "large": "vitl"}

    if model_size not in encoder_map:
        raise ValueError(f"Invalid model size: {model_size}. Choose from: small, base, large")

    encoder = encoder_map[model_size]

    try:
        # Import depth_anything_v2 (install with: uv pip install depth-anything-v2)
        from depth_anything_v2.depth_anything_v2.dpt import DepthAnythingV2

        # Initialize model with correct architecture for SMALL model
        if model_size == "small":
            model = DepthAnythingV2(encoder=encoder, features=64, out_channels=[48, 96, 192, 384])
        elif model_size == "base":
            model = DepthAnythingV2(encoder=encoder, features=128, out_channels=[96, 192, 384, 768])
        else:
            model = DepthAnythingV2(
                encoder=encoder, features=256, out_channels=[256, 512, 1024, 1024]
            )

        # Download pre-trained weights
        from huggingface_hub import hf_hub_download

        model_configs = {
            "vits": "depth_anything_v2_vits.pth",
            "vitb": "depth_anything_v2_vitb.pth",
            "vitl": "depth_anything_v2_vitl.pth",
        }

        checkpoint_path = hf_hub_download(
            repo_id="depth-anything/Depth-Anything-V2-Small"
            if encoder == "vits"
            else "depth-anything/Depth-Anything-V2-Base"
            if encoder == "vitb"
            else "depth-anything/Depth-Anything-V2-Large",
            filename=model_configs[encoder],
        )

        # Load weights
        state_dict = torch.load(checkpoint_path, map_location="cpu")
        model.load_state_dict(state_dict)
        model.eval()

        print(f"✓ Model loaded successfully")
        return model

    except ImportError:
        print("ERROR: depth-anything-v2 package not found.")
        print("\nPlease install it with:")
        print("  uv pip install git+https://github.com/DepthAnything/Depth-Anything-V2.git")
        raise


def export_to_onnx(model, output_path="depth_anything_v2_small.onnx", input_size=518):
    """
    Exports the PyTorch model to ONNX format.

    Args:
        model: PyTorch model to export
        output_path: Where to save the ONNX file
        input_size: Input image size (default 518x518 for Depth-Anything-V2)
    """
    print(f"\nExporting to ONNX format...")
    print(f"Input size: {input_size}x{input_size}")
    print(f"Output path: {output_path}")

    # Create dummy input
    dummy_input = torch.randn(1, 3, input_size, input_size)

    # Export to ONNX
    torch.onnx.export(
        model,
        dummy_input,
        output_path,
        export_params=True,
        opset_version=14,
        do_constant_folding=True,
        input_names=["input"],
        output_names=["depth"],
        dynamic_axes={"input": {0: "batch_size"}, "depth": {0: "batch_size"}},
    )

    print(f"✓ ONNX export complete")

    # Verify the exported model
    print("\nVerifying ONNX model...")
    onnx_model = onnx.load(output_path)
    onnx.checker.check_model(onnx_model)
    print("✓ ONNX model is valid")

    # Print model info
    file_size = Path(output_path).stat().st_size / (1024 * 1024)
    print(f"\nModel file size: {file_size:.1f} MB")
    print(f"Ready to use with DepthClockWallpaper!")


def main():
    parser = argparse.ArgumentParser(description="Export Depth-Anything-V2 model to ONNX format")
    parser.add_argument(
        "--model-size",
        type=str,
        default="small",
        choices=["small", "base", "large"],
        help="Model size to export (default: small)",
    )
    parser.add_argument(
        "--output",
        type=str,
        default=None,
        help="Output ONNX file path (default: depth_anything_v2_{size}.onnx)",
    )
    parser.add_argument(
        "--input-size", type=int, default=1036, help="Input image size (default: 1036)"
    )

    args = parser.parse_args()

    # Set output path
    output_path = args.output or f"depth_anything_v2_{args.model_size}.onnx"

    print("=" * 60)
    print("Depth-Anything-V2 ONNX Exporter")
    print("=" * 60)

    try:
        # Download model
        model = download_model(args.model_size)

        # Export to ONNX
        export_to_onnx(model, output_path, args.input_size)

        print("\n" + "=" * 60)
        print("SUCCESS!")
        print("=" * 60)
        print(f"\nNext steps:")
        print(f"1. Copy '{output_path}' to your DepthClockWallpaper directory")
        print(f"2. Update Program.cs if you used a different model size")
        print(f"3. Build and run the application")

    except Exception as e:
        print(f"\nERROR: {e}")
        return 1

    return 0


if __name__ == "__main__":
    exit(main())
