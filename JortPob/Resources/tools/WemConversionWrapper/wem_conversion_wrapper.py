import os
import sys
import shutil
import subprocess
import xml.etree.ElementTree as ET

def find_wwise_console_path():
    audiokinetic_path = r"C:\Audiokinetic"
    if not os.path.exists(audiokinetic_path):
        print("Wwise 2023 is not installed. Please install Wwise 2023 and try again.")
        sys.exit(1)

    wwise_folders = [folder for folder in os.listdir(audiokinetic_path) if "Wwise" in folder]
    if not wwise_folders:
        print("No Wwise installation found. Please install Wwise 2023 and try again.")
        sys.exit(1)

    wwise_folder = None
    for folder in wwise_folders:
        if "Wwise2023" in folder:
            wwise_folder = folder
            break

    if not wwise_folder:
        wwise_folder = wwise_folders[0]
        print(f"Warning: Using Wwise version {wwise_folder}, which may not be fully supported.")

    wwise_console_path = os.path.join(audiokinetic_path, wwise_folder, "Authoring", "x64", "Release", "bin", "WwiseConsole.exe")
    if not os.path.exists(wwise_console_path):
        print(f"WwiseConsole.exe not found in the expected location: {wwise_console_path}")
        sys.exit(1)

    return wwise_console_path

def create_wwise_project(root_audio_dir, wwise_console_path, project_dir, project_file):
    if not os.path.exists(project_dir) or not os.path.exists(project_file):
        create_project_args = [wwise_console_path, "create-new-project", project_file, "--platform", "Windows"]
        subprocess.run(create_project_args, check=True)

def convert_wav_to_wem(wav_file, root_audio_dir, wwise_console_path):
    print("fuck YOU 1")
    wav_file_name = os.path.basename(wav_file)
    input_folder = os.path.join(root_audio_dir, "wwise", wav_file_name[:-4], "source")
    os.makedirs(input_folder, exist_ok=True)
    print("fuck YOU 2")

    # Copy the WAV file to the input folder
    shutil.copy2(wav_file, input_folder)
    print("fuck YOU 3")
    # Generate the XML file
    xml_filepath = os.path.join(input_folder, "to_convert.wsources")
    xml_data = f"<?xml version='1.0' encoding='UTF-8'?>\n<ExternalSourcesList SchemaVersion=\"1\" Root=\"{input_folder}\"><Source Path=\"{wav_file_name}\" Conversion=\"Vorbis Quality High\" /></ExternalSourcesList>"
    with open(xml_filepath, "w") as file:
        file.write(xml_data)
        
    print("fuck YOU 4")


    # Call WwiseConsole to convert the WAV to WEM
    project_dir = os.path.join(root_audio_dir, "wwise", wav_file_name[:-4], "conversion-project")
    project_file = os.path.join(root_audio_dir, "wwise", wav_file_name[:-4], "conversion-project", "conversion-project.wproj")
    create_wwise_project(root_audio_dir, wwise_console_path, project_dir, project_file)
    command = [
        wwise_console_path,
        "convert-external-source",
        project_file,
        "--source-file",
        xml_filepath,
        "--output",
        "Windows",
        input_folder
    ]
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    print("fuck YOU 5")
    stdout, stderr = process.communicate()

    # Print the subprocess output
    print("Subprocess output:")
    print(stdout)
    print("fuck YOU 6")

    # Print the subprocess error (if any)
    if stderr:
        print("Subprocess error:")
        print(stderr)

    # Check the subprocess return code
    if process.returncode != 0:
        raise subprocess.CalledProcessError(process.returncode, command)
    print("fuck YOU 7")

    # Move the converted WEM file next to the original WAV
    wem_filename = os.path.splitext(os.path.basename(wav_file))[0] + ".wem"
    wem_filepath = os.path.join(input_folder, wem_filename)
    new_wem_filepath = os.path.join(os.path.dirname(wav_file), wem_filename)
    shutil.move(wem_filepath, new_wem_filepath)
    print("fuck YOU 8")

def main():
    wwise_console_path = find_wwise_console_path()
    root_audio_dir = os.path.dirname(os.path.abspath(sys.argv[1]))
    
    print(wwise_console_path)
    print(root_audio_dir)
    print(sys.argv[1])

    for wav_file in sys.argv[1:]:
        if wav_file.lower().endswith(".wav"):
            convert_wav_to_wem(wav_file, root_audio_dir, wwise_console_path)
        else:
            print(f"Skipping non-WAV file: {wav_file}")
            
main()