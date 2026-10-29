from rate_arms import configure,run,config,original
from schema_native_helpers import unload
try:
 configure('rate25-continued',25,'FT Food Routines PR3 OB Rate25')
 run('rate25-continued',28,'FT Food Routines PR3 OB Rate25Extended')
finally:
 unload('default-extended-restore');config.write_bytes(original)
