import bpy
import addon_utils
addon_utils.enable('blender_mcp', default_set=False, persistent=True)
import blender_mcp
blender_mcp._blendermcp_ensure_server_running()
print('RYUTA: Blender bridge startup requested', flush=True)
