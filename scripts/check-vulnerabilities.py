#!/usr/bin/env python3
"""Fail CI if NuGet reports vulnerabilities or an incomplete scan."""
import json, sys
report=json.load(open(sys.argv[1], encoding='utf-8-sig'))
if report.get('problems') or not report.get('projects'):
    sys.exit('NuGet scan failed or returned no projects.')
packages=[]
for project in report['projects']:
    for framework in project.get('frameworks',[]):
        for group in ('topLevelPackages','transitivePackages'):
            for package in framework.get(group,[]):
                if package.get('vulnerabilities'):
                    packages.append(package.get('id','unknown'))
if packages:
    sys.exit('Vulnerable dependencies: '+', '.join(sorted(set(packages))))
print('NuGet: no vulnerable packages reported in the scanned graph.')
