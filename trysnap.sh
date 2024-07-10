# This will remove oneware, build a new oneware snap, install and start it
sudo snap remove oneware
./buildsnap.sh
snapFile=oneware_$(grep -oP '<Version>\K[^<]+' ./build/props/Base.props)_amd64.snap
sudo snap install --dangerous --classic $snapFile
rm ./$snapFile
oneware