// this file is in the public domain
%module vulkan
%insert("include")
%{
#include <vulkan/vulkan.h>
#include <vulkan/vulkan_core.h>
%}

%apply unsigned long long { uint64_t };
%apply unsigned long { size_t };
%apply int { int32_t };
%apply unsigned int { uint32_t };
%apply SWIGTYPE * { VkDeviceMemory,VkBuffer,VkBufferView,VkCommandPool,VkDebugReportCallbackEXT,VkDescriptorPool,VkDescriptorSetLayout,VkDisplayKHR,VkDisplayModeKHR,VkEvent,VkFence,VkFramebuffer,VkImage,VkImageView,VkPipeline,VkPipelineCache,VkPipelineLayout,VkQueryPool,VkRenderPass,VkSampler,VkSemaphore,VkShaderModule,VkSurfaceKHR,VkSwapchainKHR,VkIndirectCommandsLayoutNVX,VkObjectTableNVX,VkDeferredOperationKHR,vkCreateRayTracingPipelinesKHR,vkCopyMemoryToAccelerationStructureKHR,VkAccelerationStructureKHR,vkDestroyAccelerationStructureKHR,VkShaderEXT,VkSamplerYcbcrConversion,VkDescriptorUpdateTemplate,VkDescriptorSet,VkPrivateDataSlot,VkVideoSessionKHR,VkVideoSessionParametersKHR,VkCuModuleNVX,VkCuFunctionNVX,VkValidationCacheEXT,VkAccelerationStructureNV,VkPerformanceConfigurationINTEL,VkIndirectCommandsLayoutNV,VkCudaModuleNV,VkMicromapEXT,VkOpticalFlowSessionNV,VkDebugUtilsMessengerEXT,VkCudaFunctionNV };
#define VKAPI_PTR
#define VKAPI_ATTR
#define VKAPI_CALL

// prep: sed -e 's,\(^.*VkAccelerationStructureInstanceKHR.*$\),// \1,g' -e 's,\(^.*VkAccelerationStructure.*NV.*$\),// \1,g'
// exec: sed -e 's/^c-library\( .*\)/cs-vocabulary vulkan``get-current also vulkan definitions``c-library\1`/g' -e 's/^end-c-library/end-c-library`previous set-current/g'  | tr '`' '\n'
%include <vulkan/vk_platform.h>
%include <vulkan/vulkan.h>
%include <vulkan/vulkan_core.h>
