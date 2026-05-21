@echo off
cd /d "%~dp0"
py -3 generate_prime_presentation.py > gen_log.txt 2>&1
if exist Module-PRIME-Presentation-Managers.pptx (
  echo SUCCESS >> gen_log.txt
) else (
  echo FAIL >> gen_log.txt
)
