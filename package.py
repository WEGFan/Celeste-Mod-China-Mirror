# -*- coding: utf-8 -*-
import re
import subprocess
import sys
import zipfile
from itertools import chain
from pathlib import Path
import os
import yaml

# load mod name and version from everest.yaml
with open('./everest.yaml', 'r', encoding='utf-8') as f:
    mod_metadata = yaml.safe_load(f.read())

mod_name = mod_metadata[0]['Name']
mod_version = mod_metadata[0]['Version']

print(mod_name, mod_version)

# update AssemblyVersion in project file to match the mod version
with open('./CelesteModChinaMirror.csproj', 'r+', encoding='utf-8') as f:
    s = f.read()
    f.seek(0)
    f.truncate()

    s = re.sub(r'^(\s+<AssemblyVersion>)(.*?)(</AssemblyVersion>)$',
               f'\\g<1>{mod_version.split("-")[0]}.0\\g<3>',
               s, flags=re.MULTILINE)

    f.write(s)

# clean the output files
subprocess.run(['dotnet', 'clean'])

# build the project
process = subprocess.run(['dotnet', 'build', '--configuration', 'Release'])
if process.returncode != 0:
    print('build failed')
    sys.exit()

# package
file_list = ['./bin/**/*', './Dialog/**/*', './everest.yaml']

os.makedirs('dist', exist_ok=True)
with zipfile.ZipFile(f'dist/{mod_name}_v{mod_version}.zip', 'w', zipfile.ZIP_DEFLATED) as f:
    for file in chain(*[Path('.').glob(i) for i in file_list]):
        # correct the case in file name
        file = file.resolve().relative_to(Path.cwd())
        print(file)
        f.write(file)
