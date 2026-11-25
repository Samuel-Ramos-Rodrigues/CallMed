#!/usr/bin/env python3
"""Compatibilidade com verificadores antigos após retorno ao visual V35."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name('check-v35-classic-visual-contracts.py')), run_name='__main__')
