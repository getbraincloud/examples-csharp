	.file	"environment.x86.s"
	.section	.rodata..L.str.1,"aMS",@progbits,1
	.type	.L.str.1, @object
.L.str.1:
	.asciz	"com.companyname.xamarinexample"
	.size	.L.str.1, 31
	.section	.data.application_config,"aw",@progbits
	.type	application_config, @object
	.p2align	2
	.global	application_config
application_config:
	/* uses_mono_llvm */
	.byte	0
	/* uses_mono_aot */
	.byte	0
	/* uses_assembly_preload */
	.byte	1
	/* is_a_bundled_app */
	.byte	0
	/* broken_exception_transitions */
	.byte	0
	/* bound_exception_type */
	.byte	1
	/* package_naming_policy */
	.zero	2
	.long	3
	/* environment_variable_count */
	.long	12
	/* system_property_count */
	.long	0
	/* android_package_name */
	.long	.L.str.1
	.size	application_config, 24
	.section	.rodata..L.str.2,"aMS",@progbits,1
	.type	.L.str.2, @object
.L.str.2:
	.asciz	"none"
	.size	.L.str.2, 5
	.section	.data.mono_aot_mode_name,"aw",@progbits
	.global	mono_aot_mode_name
mono_aot_mode_name:
	.long	.L.str.2
	.section	.rodata..L.str.3,"aMS",@progbits,1
	.type	.L.str.3, @object
.L.str.3:
	.asciz	"MONO_GC_PARAMS"
	.size	.L.str.3, 15
	.section	.rodata..L.str.4,"aMS",@progbits,1
	.type	.L.str.4, @object
.L.str.4:
	.asciz	"major=marksweep-conc"
	.size	.L.str.4, 21
	.section	.rodata..L.str.5,"aMS",@progbits,1
	.type	.L.str.5, @object
.L.str.5:
	.asciz	"MONO_LOG_LEVEL"
	.size	.L.str.5, 15
	.section	.rodata..L.str.6,"aMS",@progbits,1
	.type	.L.str.6, @object
.L.str.6:
	.asciz	"error"
	.size	.L.str.6, 6
	.section	.rodata..L.str.7,"aMS",@progbits,1
	.type	.L.str.7, @object
.L.str.7:
	.asciz	"XAMARIN_BUILD_ID"
	.size	.L.str.7, 17
	.section	.rodata..L.str.8,"aMS",@progbits,1
	.type	.L.str.8, @object
.L.str.8:
	.asciz	"d6164503-b588-4507-9330-34bc921249dc"
	.size	.L.str.8, 37
	.section	.rodata..L.str.9,"aMS",@progbits,1
	.type	.L.str.9, @object
.L.str.9:
	.asciz	"XA_HTTP_CLIENT_HANDLER_TYPE"
	.size	.L.str.9, 28
	.section	.rodata..L.str.10,"aMS",@progbits,1
	.type	.L.str.10, @object
.L.str.10:
	.asciz	"Xamarin.Android.Net.AndroidClientHandler"
	.size	.L.str.10, 41
	.section	.rodata..L.str.11,"aMS",@progbits,1
	.type	.L.str.11, @object
.L.str.11:
	.asciz	"XA_TLS_PROVIDER"
	.size	.L.str.11, 16
	.section	.rodata..L.str.12,"aMS",@progbits,1
	.type	.L.str.12, @object
.L.str.12:
	.asciz	"btls"
	.size	.L.str.12, 5
	.section	.rodata..L.str.13,"aMS",@progbits,1
	.type	.L.str.13, @object
.L.str.13:
	.asciz	"__XA_PACKAGE_NAMING_POLICY__"
	.size	.L.str.13, 29
	.section	.rodata..L.str.14,"aMS",@progbits,1
	.type	.L.str.14, @object
.L.str.14:
	.asciz	"LowercaseCrc64"
	.size	.L.str.14, 15
	.section	.data.app_environment_variables,"aw",@progbits
	.type	app_environment_variables, @object
	.p2align	2
	.global	app_environment_variables
app_environment_variables:
	.long	.L.str.3
	.long	.L.str.4
	.long	.L.str.5
	.long	.L.str.6
	.long	.L.str.7
	.long	.L.str.8
	.long	.L.str.9
	.long	.L.str.10
	.long	.L.str.11
	.long	.L.str.12
	.long	.L.str.13
	.long	.L.str.14
	.size	app_environment_variables, 48
	.section	.data.app_system_properties,"aw",@progbits
	.type	app_system_properties, @object
	.p2align	2
	.global	app_system_properties
app_system_properties:
	.size	app_system_properties, 0
