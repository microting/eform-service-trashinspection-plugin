#!/bin/bash

cd ~

if [ -d "Documents/workspace/microting/eform-service-trashinspection-plugin/ServiceTrashInspectionPlugin" ]; then
	rm -fR Documents/workspace/microting/eform-service-trashinspection-plugin/ServiceTrashInspectionPlugin
fi

cp -av Documents/workspace/microting/eform-debian-service/Plugins/ServiceTrashInspectionPlugin Documents/workspace/microting/eform-service-trashinspection-plugin/ServiceTrashInspectionPlugin
