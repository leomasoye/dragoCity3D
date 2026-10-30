import bpy
import bmesh
import math
import os
import sys

def main():
    print("--- [Blender Dragon Generator] Starting procedural dragon generation ---")
    
    # 1. Reset scene
    bpy.ops.wm.read_factory_settings(use_empty=True)
    
    # 2. Build Dragon Mesh using BMesh
    mesh_data = bpy.data.meshes.new("DragonMesh")
    dragon_obj = bpy.data.objects.new("Dragon", mesh_data)
    bpy.context.collection.objects.link(dragon_obj)
    
    bm = bmesh.new()
    
    def add_box(center, size):
        cx, cy, cz = center
        sx, sy, sz = size[0]*0.5, size[1]*0.5, size[2]*0.5
        v1 = bm.verts.new((cx - sx, cy - sy, cz - sz))
        v2 = bm.verts.new((cx + sx, cy - sy, cz - sz))
        v3 = bm.verts.new((cx + sx, cy + sy, cz - sz))
        v4 = bm.verts.new((cx - sx, cy + sy, cz - sz))
        v5 = bm.verts.new((cx - sx, cy - sy, cz + sz))
        v6 = bm.verts.new((cx + sx, cy - sy, cz + sz))
        v7 = bm.verts.new((cx + sx, cy + sy, cz + sz))
        v8 = bm.verts.new((cx - sx, cy + sy, cz + sz))
        
        # 6 faces
        bm.faces.new((v1, v2, v3, v4))
        bm.faces.new((v5, v8, v7, v6))
        bm.faces.new((v1, v5, v6, v2))
        bm.faces.new((v2, v6, v7, v3))
        bm.faces.new((v3, v7, v8, v4))
        bm.faces.new((v4, v8, v5, v1))

    # --- BODY (along Y-axis: +Y is Forward, +Z is Up, +X is Right) ---
    # Torso
    add_box((0.0, 0.0, 0.5), (1.2, 2.2, 1.0))
    # Chest
    add_box((0.0, 0.6, 0.7), (1.0, 1.0, 1.1))
    # Neck & Head
    add_box((0.0, 1.4, 0.9), (0.7, 1.0, 0.7))
    add_box((0.0, 2.2, 1.2), (0.9, 1.2, 0.8))
    # Snout
    add_box((0.0, 2.9, 1.0), (0.6, 0.8, 0.5))
    # Horns (Left and Right)
    add_box((-0.4, 2.0, 1.8), (0.15, 0.2, 0.6))
    add_box((0.4, 2.0, 1.8), (0.15, 0.2, 0.6))
    
    # Tail (segmented tapering back)
    add_box((0.0, -1.5, 0.5), (0.9, 1.2, 0.7))
    add_box((0.0, -2.5, 0.6), (0.6, 1.2, 0.5))
    add_box((0.0, -3.5, 0.8), (0.4, 1.2, 0.35))
    # Tail fin / spade
    add_box((0.0, -4.2, 1.0), (0.1, 0.6, 0.6))
    
    # --- WINGS ---
    # Left Wing (X negative)
    add_box((-1.4, 0.2, 0.9), (1.6, 0.3, 0.25))
    add_box((-2.6, -0.2, 1.1), (1.2, 0.25, 0.2))
    
    # Wing membrane function
    def add_wing_membrane(side_sign):
        p_base = bm.verts.new((side_sign * 0.6, 0.2, 0.8))
        p_joint = bm.verts.new((side_sign * 2.0, 0.3, 1.1))
        p_tip = bm.verts.new((side_sign * 3.5, -0.2, 1.3))
        p_mid_back = bm.verts.new((side_sign * 2.5, -1.0, 0.7))
        p_inner_back = bm.verts.new((side_sign * 1.0, -0.8, 0.6))
        
        bm.faces.new((p_base, p_joint, p_inner_back))
        bm.faces.new((p_joint, p_mid_back, p_inner_back))
        bm.faces.new((p_joint, p_tip, p_mid_back))

    add_wing_membrane(-1.0) # Left wing (X-)
    
    # Right Wing (X positive)
    add_box((1.4, 0.2, 0.9), (1.6, 0.3, 0.25))
    add_box((2.6, -0.2, 1.1), (1.2, 0.25, 0.2))
    add_wing_membrane(1.0)  # Right wing (X+)

    # Finalize BMesh to Mesh
    bm.to_mesh(mesh_data)
    bm.free()
    
    # Recalculate normals
    bpy.context.view_layer.objects.active = dragon_obj
    dragon_obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')

    # 3. Create Armature
    arm_data = bpy.data.armatures.new("DragonArmatureData")
    arm_obj = bpy.data.objects.new("DragonArmature", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')
    
    # Root bone (Head/Torso/Tail)
    root_bone = arm_data.edit_bones.new("Root")
    root_bone.head = (0.0, -1.0, 0.5)
    root_bone.tail = (0.0, 1.5, 0.8)
    
    # Wing_L bone
    wing_l_bone = arm_data.edit_bones.new("Wing_L")
    wing_l_bone.head = (-0.6, 0.2, 0.8)
    wing_l_bone.tail = (-3.5, 0.0, 1.2)
    wing_l_bone.parent = root_bone
    
    # Wing_R bone
    wing_r_bone = arm_data.edit_bones.new("Wing_R")
    wing_r_bone.head = (0.6, 0.2, 0.8)
    wing_r_bone.tail = (3.5, 0.0, 1.2)
    wing_r_bone.parent = root_bone
    
    bpy.ops.object.mode_set(mode='OBJECT')

    # 4. Bind Mesh to Armature & Assign Vertex Groups based on coordinates
    vg_root = dragon_obj.vertex_groups.new(name="Root")
    vg_wing_l = dragon_obj.vertex_groups.new(name="Wing_L")
    vg_wing_r = dragon_obj.vertex_groups.new(name="Wing_R")
    
    for v in mesh_data.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        if x < -0.7:
            # Left wing
            weight_wing = min(1.0, (abs(x) - 0.7) / 1.5)
            weight_root = 1.0 - weight_wing
            vg_wing_l.add([v.index], weight_wing, 'REPLACE')
            if weight_root > 0.0:
                vg_root.add([v.index], weight_root, 'REPLACE')
        elif x > 0.7:
            # Right wing
            weight_wing = min(1.0, (x - 0.7) / 1.5)
            weight_root = 1.0 - weight_wing
            vg_wing_r.add([v.index], weight_wing, 'REPLACE')
            if weight_root > 0.0:
                vg_root.add([v.index], weight_root, 'REPLACE')
        else:
            # Main body / Head / Tail
            vg_root.add([v.index], 1.0, 'REPLACE')
        
    # Armature modifier
    mod = dragon_obj.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = arm_obj
    mod.use_vertex_groups = True
    dragon_obj.parent = arm_obj

    # 5. Stylized Materials
    mat = bpy.data.materials.new(name="Dragon_Skin")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        # Fiery Crimson / Draconic Red
        bsdf.inputs["Base Color"].default_value = (0.8, 0.15, 0.08, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.35
    dragon_obj.data.materials.append(mat)

    # 6. Export FBX
    output_dir = os.path.abspath("./DragonCityGame/UnityProject/Assets/_Game/Models")
    os.makedirs(output_dir, exist_ok=True)
    fbx_path = os.path.join(output_dir, "dragon.fbx")
    
    print(f"Exporting to: {fbx_path}")
    
    # Select both armature and mesh
    bpy.ops.object.select_all(action='DESELECT')
    arm_obj.select_set(True)
    dragon_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    
    # Blender FBX Export
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        check_existing=False,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        armature_nodetype='NULL',
        bake_anim=False
    )
    
    if os.path.exists(fbx_path) and os.path.getsize(fbx_path) > 1000:
        print(f"--- SUCCESS: dragon.fbx created successfully ({os.path.getsize(fbx_path)} bytes) ---")
    else:
        print(f"--- ERROR: dragon.fbx failed to generate properly ---")
        sys.exit(1)

if __name__ == "__main__":
    main()
